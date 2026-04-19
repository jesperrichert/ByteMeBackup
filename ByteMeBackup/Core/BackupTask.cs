using System.Formats.Tar;
using System.IO.Compression;
using ByteMeBackup.Configuration;
using ByteMeBackup.Services;
using Renci.SshNet;
using Spectre.Console;

namespace ByteMeBackup.Core;

public class BackupTask
{
    private readonly BackupConfig BackupConfig;
    private readonly RemoteServerConfig RemoteServerConfig;
    private readonly DiscordWebhookLogService LogService;

    public BackupTask(BackupConfig backupConfig, RemoteServerConfig remoteServerConfig,
        DiscordWebhookLogService logService)
    {
        BackupConfig = backupConfig;
        RemoteServerConfig = remoteServerConfig;
        LogService = logService;
    }

    public async Task Run()
    {
        try
        {
            await LogAsync("Starting backup task...", "[bold gray]Starting backup task...[/]");
            await LogAsync("Backup for Config", $"Start Backup with path {BackupConfig.BackupPath}");

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var zipFileName = $"{BackupConfig.BackupPrefix}{timestamp}.tar.gz";
            var tempBackupPath = Path.GetTempPath() + BackupConfig.BackupPrefix + timestamp;
            var tempZipPath = Path.Combine(Path.GetTempPath(), zipFileName);

            await LogAsync($"Choosing Backup Mode {BackupConfig.BackupMode.ToString()}",
                $"[white]Choosing Backup Mode \"{BackupConfig.BackupMode.ToString()}\"[/]");
            switch (BackupConfig.BackupMode)
            {
                case BackupMode.Files:
                {
                    await LogAsync("-# Creating Temporary Backup Folder", $"[gray]Creating Temporary Backup Folder[/]");
                    var uuid = $"{Guid.CreateVersion7().ToString()}-{timestamp}";
                    var tempBackupFolder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), uuid));

                    try
                    {
                        await LogAsync("-# Collecting Files for File Backup.",
                            $"[gray]Collecting Files for File Backup.[/]");
                        foreach (var path in BackupConfig.BackupFiles)
                        {
                            await LogAsync($"-# Copy File {path} to backup.", $"[gray]Copy File {path} to backup.[/]");
                            File.Copy(path, Path.Combine(tempBackupFolder.FullName, path.Split("/").Last()));
                        }

                        await LogAsync("-# Creating tag.gz file for backup.",
                            "[gray]Creating tag.gz file for backup.[/]");
                        await TarFile.CreateFromDirectoryAsync(tempBackupFolder.FullName, tempZipPath, true,
                            new CancellationToken(false));
                    }
                    finally
                    {
                        foreach (var fileInfo in tempBackupFolder.GetFiles())
                        {
                            fileInfo.Delete();
                        }
                    }
                }
                    break;
                case BackupMode.Folder:
                {
                    await LogAsync("-# Creating tag.gz file for backup.",
                        "[gray]Creating tag.gz file for backup.");
                    await TarFile.CreateFromDirectoryAsync(BackupConfig.BackupPath, tempZipPath, true,
                        new CancellationToken(false));
                }
                    break;
            }

            var mode = BackupConfig.BackupMode == BackupMode.Files
                ? $"Files: \n{string.Join(", \n> ", BackupConfig.BackupFiles)}"
                : $"Directory: {BackupConfig.BackupPath}";
            await LogAsync($"""
                            **Backup created successfully!** 
                            > Mode: {BackupConfig.BackupMode.ToString()}
                            > {mode}
                            > Backup-File: {zipFileName}
                            > Temporary Path: {tempZipPath}
                            > Timestamp: {timestamp}
                            """,
                $"[green]Backup created: {tempZipPath}[/]"
            );

            switch (BackupConfig.BackupType)
            {
                case BackupType.MountedDrive:
                    await HandleMountedDriveBackup(tempZipPath, zipFileName);
                    break;

                case BackupType.ToServer:
                    await HandleSftpBackup(tempZipPath, zipFileName);
                    break;

                default:
                    AnsiConsole.WriteException(
                        new NotSupportedException($"Backup type {BackupConfig.BackupType} is not supported."));
                    await LogService.SendLogAsync($"❌ **Unsupported backup type:** {BackupConfig.BackupType}");
                    return;
            }

            try
            {
                File.Delete(tempZipPath);
                await LogAsync(
                    "-# Temporary zip file deleted successfully!",
                    $"[grey]Deleted temporary file: {tempZipPath}[/]\n[grey]Deleted temporary file: {tempBackupPath}[/]"
                );
            }
            catch (Exception e)
            {
                await HandleErrorAsync(e, "deleting temporary zip file");
            }

            await LogAsync("✅ **Backup task finished successfully.**", "[bold green]Backup task completed.[/]");
        }
        catch (Exception e)
        {
            await HandleErrorAsync(e, "running the backup task");
        }
    }

    private async Task HandleMountedDriveBackup(string sourceZipPath, string fileName)
    {
        try
        {
            var targetPath = Path.Combine(BackupConfig.MountedDrivePath, fileName);

            if (!Directory.Exists(BackupConfig.MountedDrivePath))
            {
                Directory.CreateDirectory(BackupConfig.MountedDrivePath);
                AnsiConsole.Markup("[bold yellow]Mounted drive directory created[/]\n");
            }

            File.Copy(sourceZipPath, targetPath, overwrite: true);

            await LogAsync($"""
                            **Backup copied to mounted drive successfully!**
                            > File: {fileName}
                            > Mounted Drive Path: {BackupConfig.MountedDrivePath}
                            """,
                "[green]Backup copied to mounted drive successfully![/]\n" +
                $"[bold white]File:[/] [blue]{fileName}[/]"
            );
        }
        catch (Exception e)
        {
            await HandleErrorAsync(e, "copying backup to mounted drive");
        }
    }

    private async Task HandleSftpBackup(string sourceZipPath, string fileName)
    {
        try
        {
            using var client =
                new SftpClient(RemoteServerConfig.IpAddress, int.Parse(RemoteServerConfig.Port),
                    RemoteServerConfig.Username, RemoteServerConfig.Password);
            client.Connect();
            await using var fileStream = File.OpenRead(sourceZipPath);
            client.UploadFile(fileStream, fileName);
            client.Disconnect();

            await LogAsync($"""
                            **Backup uploaded to remote server successfully!**
                            > File: {fileName}
                            > Server: {RemoteServerConfig.IpAddress}:{RemoteServerConfig.Port}
                            """,
                "[green]Backup uploaded to remote server successfully![/]\n" +
                $"[bold white]File:[/] [blue]{fileName}[/]"
            );
        }
        catch (Exception e)
        {
            await HandleErrorAsync(e, "uploading backup via SFTP");
        }
    }

    private async Task LogAsync(string message, string consoleMarkup = null!)
    {
        if (!string.IsNullOrWhiteSpace(consoleMarkup))
            AnsiConsole.Markup(consoleMarkup + "\n");

        await LogService.SendLogAsync(message);
    }

    private async Task HandleErrorAsync(Exception e, string context)
    {
        AnsiConsole.Markup($"[red]Error during {context}[/]\n");
        AnsiConsole.WriteException(e);
        await LogService.SendLogAsync($"❌ **Error during {context}:** ``{e.Message}``");
    }
}