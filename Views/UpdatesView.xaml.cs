using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace mindcraft_ce.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UpdatesView : Page
    {
        private CancellationTokenSource _logWatcherCts;

        public UpdatesView()
        {
            InitializeComponent();
        }

        public static async Task<JObject> GetMetadata()
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile metaDataFile = await localFolder.GetFileAsync("metadata.json");
                string content = await FileIO.ReadTextAsync(metaDataFile);
                return JObject.Parse(content);
            }
            catch (FileNotFoundException)
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile metaDataFile = await localFolder.CreateFileAsync("metadata.json", CreationCollisionOption.OpenIfExists);
                JObject initialContent = new JObject
                {
                    { "version", null },
                    { "installed", false },
                    { "installation_path", null }
                };
                await FileIO.WriteTextAsync(metaDataFile, initialContent.ToString());
                return initialContent;
            }
        }

        public static async Task<JObject> GetSettings()
        {
            JObject metadata = await GetMetadata();
            if (metadata["installed"]?.Value<bool>() != true)
                return new JObject();

            var installationPath = metadata["installation_path"]?.Value<string>();
            if (string.IsNullOrEmpty(installationPath))
                return new JObject();

            try
            {
                var settingsPath = Path.Combine(installationPath, "settings.json");
                StorageFile settingsFile = await StorageFile.GetFileFromPathAsync(settingsPath);
                string content = await FileIO.ReadTextAsync(settingsFile);
                return JObject.Parse(content);
            }
            catch (FileNotFoundException)
            {
                return new JObject();
            }
        }

        public static JObject GetMetadataSync()
        {
            return Task.Run(() => GetMetadata()).GetAwaiter().GetResult();
        }

        public static JObject GetSettingsSync()
        {
            return Task.Run(() => GetSettings()).GetAwaiter().GetResult();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateDisplay();
        }

        private async void UpdateDisplay()
        {
            JObject metadata = await GetMetadata();
            if (metadata["installed"]?.Value<bool>() == true)
            {
                string currentVersion = metadata["version"]?.Value<string>();
                ShowMessage("Version: " + currentVersion + "\n\nChecking for updates...");
                string latestVersion = await GetLatestVersion();
                if (latestVersion == currentVersion)
                {
                    ShowMessage("Version: " + currentVersion + "\n\nYou are using the latest version.");
                    installButton.Content = "Uninstall";
                    installButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ShowMessage("Version: " + currentVersion + "\nA new version is available: " + latestVersion + ".\n\nClick the button below to update.");
                    installButton.Content = "Update";
                    installButton.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ShowMessage("mindcraft-ce has not been installed. Click the button to begin installation.");
                installButton.Content = "Install";
                installButton.Visibility = Visibility.Visible;
            }
        }

        private async Task<string> GetLatestVersion()
        {
            using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0");

            var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com/mindcraft-ce/mindcraft-ce/releases/latest");
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == System.Net.HttpStatusCode.Found)
            {
                string redirectUrl = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(redirectUrl) && redirectUrl.Contains("/tag/"))
                {
                    return redirectUrl.Split("/tag/")[1];
                }
            }
            throw new Exception("Could not determine the latest version from GitHub.");
        }

        private void ShowMessage(string message)
        {
            Debug.WriteLine(message);
            messageTextBlock.Text = message;
            messageTextBlock.Visibility = Visibility.Visible;
        }

        private void LogMessage(string message)
        {
            Debug.WriteLine(message);
            DispatcherQueue.TryEnqueue(() =>
            {
                logTextBlock.Text += message + "\n";
                logTextBlock.Visibility = Visibility.Visible;
                logScrollViewer.ChangeView(null, logScrollViewer.ExtentHeight, null);
            });
        }

        private async Task WatchLogFileAsync(string path, CancellationToken token)
        {
            await Task.Delay(200, token); // Wait for file creation
            long lastPosition = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (fs.Length > lastPosition)
                        {
                            fs.Position = lastPosition;
                            using var reader = new StreamReader(fs);
                            string newLines = await reader.ReadToEndAsync(token);
                            if (!string.IsNullOrEmpty(newLines))
                            {
                                foreach (string line in newLines.Split("\n"))
                                {
                                    LogMessage(line);
                                }
                            }
                            lastPosition = fs.Position;
                        }
                    }
                }
                catch (IOException) { /* Ignore file lock, will retry */ }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    DispatcherQueue.TryEnqueue(() => logTextBlock.Text += $"\nError reading log file: {ex.Message}\n");
                }
                await Task.Delay(500, token);
            }
        }

        private async Task UninstallMindcraftCe()
        {
            copyButton.Visibility = Visibility.Visible;
            installButton.IsEnabled = false;
            logTextBlock.Text = ""; // Clear previous logs
            var metadata = await GetMetadata();
            if (metadata["installed"]?.Value<bool>() != true)
            {
                Debug.WriteLine("mindcraft-ce is not installed. there is nothing to uninstall.");
                installButton.IsEnabled = true;
                return;
            }
            var installationPath = metadata["installation_path"]?.Value<string>();
            if (string.IsNullOrEmpty(installationPath) || !Directory.Exists(installationPath))
            {
                LogMessage("Installation path is invalid or does not exist.");
                installButton.IsEnabled = true;
                return;
            }
            try
            {
                LogMessage("Removing installation directory...");
                (await StorageFolder.GetFolderFromPathAsync(installationPath)).DeleteAsync(StorageDeleteOption.PermanentDelete);
                LogMessage("Installation directory removed successfully.");
                metadata["installed"] = false;
                metadata["installation_path"] = null;
                await File.WriteAllTextAsync(Path.Combine(ApplicationData.Current.LocalFolder.Path, "metadata.json"), metadata.ToString());
                LogMessage("\n--- Uninstallation Complete ---");
                ShowMessage("mindcraft-ce has been uninstalled successfully.");
                installButton.Visibility = Visibility.Collapsed;
                App.MainWindowInstance.UpdateNavigationView();
            }
            catch (Exception ex)
            {
                LogMessage($"\n--- UNINSTALLATION ERROR ---");
                LogMessage(ex.Message);
            }
            finally
            {
                installButton.IsEnabled = true;
                App.MainWindowInstance.UpdateNavigationView();
                DispatcherQueue.TryEnqueue(() => { UpdateDisplay(); });
            }
        }

        private async Task InstallMindcraftCe()
        {
            copyButton.Visibility = Visibility.Visible;
            installButton.IsEnabled = false;
            logTextBlock.Text = ""; // Clear previous logs
            DispatcherQueue.TryEnqueue(()=> { ShowMessage("Installing mindcraft-ce. Please do not leave the Updates tab."); } );

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            string psScriptPath = Path.Combine(localFolder.Path, "install.ps1");
            string logFilePath = Path.Combine(localFolder.Path, "install.log");

            if (File.Exists(logFilePath)) File.Delete(logFilePath);

            _logWatcherCts = new CancellationTokenSource();
            _ = WatchLogFileAsync(logFilePath, _logWatcherCts.Token);

            try
            {
                LogMessage("--- Preparing installation ---");
                string latestVersion = await GetLatestVersion();
                installButton.Content = "Installing...";

                // This is the entire installation logic embedded in a PowerShell script.
                string script = GetPowerShellScript();
                await File.WriteAllTextAsync(psScriptPath, script);

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{psScriptPath}\" -AppLocalFolder \"{localFolder.Path}\" -LogFilePath \"{logFilePath}\" -LatestVersion \"{latestVersion}\"",
                        Verb = "runas", // Triggers UAC for admin rights
                        UseShellExecute = true,
                        CreateNoWindow = true
                    }
                };

                LogMessage("Administrator privileges are required. Please approve the UAC prompt.");
                process.Start();
                await process.WaitForExitAsync();

                JObject metadata = await GetMetadata();
                if (metadata["installed"]?.Value<bool>() == true)
                {
                    LogMessage("\n--- Installation Complete ---");
                    DispatcherQueue.TryEnqueue(() => { UpdateDisplay(); });
                }
                else
                {
                    throw new Exception("Installation process failed. Check logs for details.");
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
            {
                LogMessage("\n--- INSTALLATION CANCELLED ---");
                LogMessage("The operation was cancelled by the user.");
                installButton.Content = "Retry Installation";
                installButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                LogMessage($"\n--- FATAL ERROR ---");
                LogMessage(ex.Message);
                installButton.Content = "Retry Installation";
                installButton.IsEnabled = true;
            }
            finally
            {
                _logWatcherCts?.Cancel();
                App.MainWindowInstance.UpdateNavigationView();
            }
        }

        private void installButton_Click(object sender, RoutedEventArgs e)
        {
            if (installButton.Content.ToString() == "Uninstall")
            {
                _ = UninstallMindcraftCe();
            }
            else
            {
                _ = InstallMindcraftCe();
            }
        }

        private string GetPowerShellScript()
        {
            return @"
[CmdletBinding()]
param (
    [Parameter(Mandatory=$true)][string]$AppLocalFolder,
    [Parameter(Mandatory=$true)][string]$LogFilePath,
    [Parameter(Mandatory=$true)][string]$LatestVersion
)

function Write-Log {
    param ([string]$Message)
    ""$Message"" | Out-File -FilePath $LogFilePath -Append
}

try {
    Write-Log '--- Starting mindcraft-ce Installation Script ---'
    $InstallationDir = Join-Path -Path $AppLocalFolder -ChildPath 'mindcraft-ce-install'


    $BackupDir = Join-Path -Path $env:TEMP -ChildPath 'mindcraft-ce-backup'
    if (Test-Path $BackupDir) { Remove-Item $BackupDir -Recurse -Force } # Clean up from any previous failed run

    if (Test-Path $InstallationDir) {
        Write-Log 'Previous installation found. Backing up user data...'
        New-Item -Path $BackupDir -ItemType Directory | Out-Null
        
        # Backup settings.json if it exists
        $settingsFile = Join-Path -Path $InstallationDir -ChildPath 'settings.json'
        if (Test-Path $settingsFile) {
            Write-Log '... Backing up settings.json'
            Move-Item -Path $settingsFile -Destination $BackupDir
        }

        # Backup keys.json if it exists
        $keysFile = Join-Path -Path $InstallationDir -ChildPath 'keys.json'
        if (Test-Path $keysFile) {
            Write-Log '... Backing up keys.json'
            Move-Item -Path $keysFile -Destination $BackupDir
        }

        # Backup .logging_consent if it exists
        $loggingConsentFile = Join-Path -Path $InstallationDir -ChildPath '.logging_consent'
        if (Test-Path $loggingConsentFile) {
            Write-Log '... Backing up .logging_consent'
            Move-Item -Path $loggingConsentFile -Destination $BackupDir
        }

        # Backup gui_agents folder if it exists
        $guiAgentsDir = Join-Path -Path $InstallationDir -ChildPath 'gui_agents'
        if (Test-Path $guiAgentsDir) {
            Write-Log '... Backing up gui_agents/*'
            Move-Item -Path $guiAgentsDir -Destination $BackupDir
        }

        # Backup bots folder if it exists
        $botsDir = Join-Path -Path $InstallationDir -ChildPath 'bots'
        if (Test-Path $botsDir) {
            Write-Log '... Backing up bots/*'
            Move-Item -Path $botsDir -Destination $BackupDir
        }

        Write-Log 'Backup complete. Removing old installation...'
        Remove-Item $InstallationDir -Recurse -Force
    }


    function Add-To-Path {
        param ([string]$PathToAdd)
        Write-Log ""Checking PATH for '$PathToAdd'""
        $machinePath = [System.Environment]::GetEnvironmentVariable('Path', 'Machine')
        if ($machinePath -notlike ""*$PathToAdd*"") {
            Write-Log 'Adding to Machine PATH...'
            $newMachinePath = ""$machinePath;$PathToAdd""
            [System.Environment]::SetEnvironmentVariable('Path', $newMachinePath, 'Machine')
            $env:Path = ""$($env:Path);$PathToAdd""
            Write-Log 'Machine PATH updated.'
        } else {
            Write-Log 'Path already exists.'
        }
    }

    # --- 1. Install Git ---
    Write-Log '--- Checking/Installing Git ---'
    if (Get-Command git -ErrorAction SilentlyContinue) {
        Write-Log 'Git is already installed.'
    } else {
        Write-Log 'Downloading Git...'
        $gitUrl = 'https://github.com/git-for-windows/git/releases/download/v2.50.1.windows.1/Git-2.50.1-64-bit.exe'
        $gitInstaller = Join-Path -Path $env:TEMP -ChildPath 'git-installer.exe'
        Invoke-WebRequest -Uri $gitUrl -OutFile $gitInstaller
        Write-Log 'Installing Git...'
        Start-Process -FilePath $gitInstaller -ArgumentList '/VERYSILENT /NORESTART' -Wait
        Add-To-Path -PathToAdd 'C:\Program Files\Git\cmd'
    }

    # --- 2. Install Node.js ---
    Write-Log '--- Checking/Installing Node.js ---'
    if ((Get-Command node -ErrorAction SilentlyContinue)) {
        Write-Log 'Node.js is already installed.'
    } else {
        Write-Log 'Downloading Node.js...'
        $nodeUrl = 'https://nodejs.org/dist/v22.17.1/node-v22.17.1-x64.msi'
        $nodeInstaller = Join-Path -Path $env:TEMP -ChildPath 'node-installer.msi'
        Invoke-WebRequest -Uri $nodeUrl -OutFile $nodeInstaller
        Write-Log 'Installing Node.js...'
        Start-Process msiexec.exe -ArgumentList ""/i `""$nodeInstaller`"" /quiet /qn /norestart"" -Wait
        Add-To-Path -PathToAdd 'C:\Program Files\nodejs\'
    }
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [System.Environment]::GetEnvironmentVariable('Path','User')
    
    # --- 3. Download and Extract Project ---
    Write-Log '--- Downloading and Extracting mindcraft-ce ---'
    $zipUrl = ""https://github.com/mindcraft-ce/mindcraft-ce/archive/refs/tags/$LatestVersion.zip""
    $zipFile = Join-Path -Path $env:TEMP -ChildPath ""mindcraft-ce-$LatestVersion.zip""
    $tempExtractDir = Join-Path -Path $env:TEMP -ChildPath 'mindcraft-ce-extract'

    if (Test-Path $InstallationDir) { Remove-Item $InstallationDir -Recurse -Force }
    if (Test-Path $tempExtractDir) { Remove-Item $tempExtractDir -Recurse -Force }
    New-Item -Path $InstallationDir -ItemType Directory | Out-Null
    
    Write-Log ""Downloading from $zipUrl...""
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipFile
    Write-Log 'Extracting archive...'
    Expand-Archive -Path $zipFile -DestinationPath $tempExtractDir -Force
    $nestedDir = Get-ChildItem -Path $tempExtractDir | Select-Object -First 1
    Get-ChildItem -Path $nestedDir.FullName | Move-Item -Destination $InstallationDir
    Remove-Item $tempExtractDir, $zipFile -Recurse -Force
    Set-Location -Path $InstallationDir

    if (Test-Path $BackupDir) {
        Write-Log '--- Restoring User Data ---'
        # Move the backed-up items into the new installation, overwriting any defaults
        Get-ChildItem -Path $BackupDir | Move-Item -Destination $InstallationDir -Force
        Write-Log 'User data restored.'
        Remove-Item $BackupDir -Recurse -Force # Clean up the backup directory
    }

    # --- 4. Configure Project Files ---
    if (-not (Test-Path 'settings.json')) {
        Write-Log 'Fresh install detected. Generating default configuration...'
        Rename-Item -Path 'keys.example.json' -NewName 'keys.json'
        (Get-Content -Path 'settings.js' -Raw) -replace '""./andy.json"",', '""./gui_agents/andy.json"",' | Set-Content -Path 'settings.js'
        New-Item -Path 'gui_agents' -ItemType Directory -Force | Out-Null
        Move-Item -Path 'andy.json' -Destination 'gui_agents'

        $nodeScript = ""import { writeFileSync } from 'fs'; import settings from './settings.js'; writeFileSync('./settings.json', JSON.stringify(settings, null, 2));""
        node -e $nodeScript

        # Write {""consent"": false} to .logging_consent
        $loggingConsentFile = Join-Path -Path $InstallationDir -ChildPath '.logging_consent'
        $loggingConsentContent = @{ consent = $false } | ConvertTo-Json
        Set-Content -Path $loggingConsentFile -Value $loggingConsentContent


        Write -Log 'Default configuration generated.'
    } else {
        Write-Log 'Existing settings.json restored. Skipping default configuration.'
    }

    # --- 5. NPM Install ---
    Write-Log '--- Running npm install ---'
    npm install --verbose *>&1 | ForEach-Object { Write-Log $_ }
    Write-Log ""'npm install' completed.""

    # --- 6. Finalize and Update Metadata ---
    Write-Log '--- Finalizing installation ---'
    $metaDataFilePath = Join-Path -Path $AppLocalFolder -ChildPath 'metadata.json'
    $metadata = @{ version = $LatestVersion; installed = $true; installation_path = $InstallationDir }
    $metadata | ConvertTo-Json | Set-Content -Path $metaDataFilePath
    Write-Log '--- mindcraft-ce has been successfully installed! ---'
}
catch {
    Write-Log ''
    Write-Log '--- FATAL SCRIPT ERROR ---'
    Write-Log $_.Exception.Message
    Write-Log $_.Exception.InvocationInfo.PositionMessage
    exit 1
}
";
        }

        private void copyButton_Click(object sender, RoutedEventArgs e)
        {
            // Copy logs
            string textToCopy = logTextBlock.Text;
            var dataPackage = new DataPackage();
            dataPackage.SetText(textToCopy);
            Clipboard.SetContent(dataPackage);
        }
    }
}