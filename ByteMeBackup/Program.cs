using ByteMeBackup;
using Spectre.Console;
using Version = ByteMeBackup.Version;

AnsiConsole.Markup($"[bold white]Welcome to ByteMeBackup v.{Version.VersionNumber}[/]\n");
AnsiConsole.Markup("[gray]Made by jespersen[/]\n\n");

AnsiConsole.Markup("[bold white]Starting ByteMeBackup Scheduler...[/]\n");

await Startup.Main();