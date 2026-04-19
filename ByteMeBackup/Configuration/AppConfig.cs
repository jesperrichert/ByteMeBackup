namespace ByteMeBackup.Configuration;

public class AppConfig
{
    public string DiscordWebhookUrl { get; set; } = string.Empty;
    public RemoteServerConfig RemoteServer { get; set; } = new RemoteServerConfig();
    public BackupConfig[] BackupConfigs { get; set; } = [];
}

public class BackupConfig
{
    public string BackupPath { get; set; } = string.Empty;
    public string[] BackupFiles { get; set; } = [];
    public BackupMode BackupMode { get; set; } = BackupMode.Folder;
    public string BackupPrefix { get; set; } = string.Empty;

    public BackupType BackupType { get; set; } = BackupType.MountedDrive;
    public string MountedDrivePath { get; set; } = string.Empty;
}

public class RemoteServerConfig
{
    public string IpAddress { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public enum BackupType
{
    ToServer,
    MountedDrive,
}

public enum BackupMode
{
    Files,
    Folder,
}