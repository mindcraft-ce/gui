using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MindcraftCE.Views
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
            Console.WriteLine($"LocalFolder: {ApplicationData.Current.LocalFolder.Path}");
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
            Console.WriteLine(message);
            messageTextBlock.Text = message;
            messageTextBlock.Visibility = Visibility.Visible;
        }

        private void LogMessage(string message)
        {
            Console.WriteLine(message);
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
                Console.WriteLine("mindcraft-ce is not installed. there is nothing to uninstall.");
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
                App.RootPage.UpdateNavigationView();
            }
            catch (Exception ex)
            {
                LogMessage($"\n--- UNINSTALLATION ERROR ---");
                LogMessage(ex.Message);
            }
            finally
            {
                installButton.IsEnabled = true;
                App.RootPage.UpdateNavigationView();
                DispatcherQueue.TryEnqueue(() => { UpdateDisplay(); });
            }
        }

        private async Task InstallMindcraftCe()
        {
            copyButton.Visibility = Visibility.Visible;
            installButton.IsEnabled = false;
            logTextBlock.Text = ""; // Clear previous logs
            DispatcherQueue.TryEnqueue(()=> { ShowMessage("Installing mindcraft-ce. Please do not leave the Updates tab."); } );

            try
            {
                LogMessage("--- Preparing installation ---");
                installButton.Content = "Installing...";

                string latestVersion = await GetLatestVersion();
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                string logFilePath = Path.Combine(localFolder.Path, "install.log");
                if (File.Exists(logFilePath)) File.Delete(logFilePath);

                _logWatcherCts = new CancellationTokenSource();
                _ = WatchLogFileAsync(logFilePath, _logWatcherCts.Token);

                string scriptContent;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    scriptContent = GetPowerShellScript();
                    await ExecuteInstallScriptAsync(scriptContent, "Windows", localFolder.Path, logFilePath, latestVersion);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    scriptContent = GetBashScript();
                    await ExecuteInstallScriptAsync(scriptContent, "Linux", localFolder.Path, logFilePath, latestVersion);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    scriptContent = GetBashScript();
                    await ExecuteInstallScriptAsync(scriptContent, "macOS", localFolder.Path, logFilePath, latestVersion);
                }
                else
                {
                    throw new PlatformNotSupportedException("Your operating system is not supported by mindcraft-ce.");
                }

               
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
            catch (Exception ex)
            {
                LogMessage($"\n--- INSTALLATION FAILED ---");
                LogMessage(ex.Message);
                installButton.Content = "Retry Installation";
                installButton.IsEnabled = true;
            }
            finally
            {
                _logWatcherCts?.Cancel();
                App.RootPage.UpdateNavigationView();
            }
        }

        private async Task ExecuteInstallScriptAsync(string scriptContent, string osPlatform, string localFolderPath, string logFilePath, string latestVersion)
        {
            string scriptPath;
            var startInfo = new ProcessStartInfo { UseShellExecute = true };
            
            string q_log = $"\"{logFilePath}\"";
            string q_local = $"\"{localFolderPath}\"";
            string q_version = $"\"{latestVersion}\"";

            switch (osPlatform)
            {
                case "Windows":
                    scriptPath = Path.Combine(localFolderPath, "install.ps1");
                    await File.WriteAllTextAsync(scriptPath, scriptContent);
                    startInfo.FileName = "powershell.exe";
                    startInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -LogFilePath {q_log} -AppLocalFolder {q_local} -LatestVersion {q_version}";
                    startInfo.Verb = "runas"; // UAC prompt
                    break;

                case "Linux":
                    scriptPath = Path.Combine(localFolderPath, "install.sh");
                    await File.WriteAllTextAsync(scriptPath, scriptContent);
                    Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit(); // Make executable
                    startInfo.FileName = "pkexec"; // Graphical sudo
                    startInfo.Arguments = $"{scriptPath} {q_log} {q_local} {q_version}";
                    break;

                case "macOS":
                    scriptPath = Path.Combine(localFolderPath, "install.sh");
                    await File.WriteAllTextAsync(scriptPath, scriptContent);
                    Process.Start("chmod", $"+x \"{scriptPath}\"")?.WaitForExit(); // Make executable
                    startInfo.FileName = "osascript";
                    startInfo.Arguments = $"-e 'do shell script \"{scriptPath} {q_log} {q_local} {q_version}\" with administrator privileges'";
                    break;

                default:
                    throw new PlatformNotSupportedException();
            }

            var process = new Process { StartInfo = startInfo };
            LogMessage("Administrator privileges are required. Please approve the prompt.");
            process.Start();
            await process.WaitForExitAsync();

            // Handle OS-specific failure codes
            if (osPlatform == "Windows" && process.ExitCode == 1223)
            {
                throw new Exception("The operation was cancelled by the user (UAC).");
            }
            
            if (process.ExitCode != 0)
            {
                throw new Exception($"The installation script failed with exit code {process.ExitCode}.");
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

        $nodeScript = ""(async () => { const fs = require('fs'); const settings = await import('./settings.js'); fs.writeFileSync('./settings.json', JSON.stringify(settings.default, null, 2)); })()""
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

        private string GetBashScript()
        {
            return @"#!/bin/bash

# Exit immediately if any command fails, making the script safer.
set -e

# --- 1. Define Parameters from Arguments ---
LOG_FILE=""$1""
APP_LOCAL_FOLDER=""$2""
LATEST_VERSION=""$3""

# --- 2. Helper Functions ---
write_log() {
    # Appends a timestamp and the message to the log file.
    echo ""$(date '+%Y-%m-%d %H:%M:%S') - $1"" >> ""$LOG_FILE""
}

install_dependency() {
    local cmd=$1
    local package=$2
    
    write_log ""--- Checking/Installing $cmd ---""
    if command -v $cmd &> /dev/null; then
        write_log ""$cmd is already installed.""
        return
    fi
    
    write_log ""$cmd not found. Attempting installation...""
    if [[ ""$(uname)"" == ""Darwin"" ]]; then # macOS
        if command -v brew &> /dev/null; then
            write_log ""Installing $package using Homebrew...""
            brew install $package >> ""$LOG_FILE"" 2>&1
        else
            write_log ""ERROR: Homebrew is not installed. Please install it to continue.""
            exit 1
        fi
    else # Linux
        if command -v apt-get &> /dev/null; then
            write_log ""Installing $package using apt-get...""
            sudo apt-get update >> ""$LOG_FILE"" 2>&1
            sudo apt-get install -y $package >> ""$LOG_FILE"" 2>&1
        elif command -v dnf &> /dev/null; then
            write_log ""Installing $package using dnf...""
            sudo dnf install -y $package >> ""$LOG_FILE"" 2>&1
        elif command -v pacman &> /dev/null; then
            write_log ""Installing $package using pacman...""
            sudo pacman -S --noconfirm $package >> ""$LOG_FILE"" 2>&1
        else
            write_log ""ERROR: Could not find a supported package manager (apt, dnf, pacman).""
            exit 1
        fi
    fi
}

install_build_deps() {
    write_log ""--- Checking for native build dependencies ---""
    if [[ ""$(uname)"" == ""Darwin"" ]]; then
        # On macOS, this is handled by Xcode Command Line Tools, which we assume is present.
        if ! command -v pkg-config &> /dev/null; then
            if command -v brew &> /dev/null; then
                write_log ""Installing pkg-config via Homebrew...""
                brew install pkg-config >> ""$LOG_FILE"" 2>&1
            fi
        fi
    else # Linux
        if command -v apt-get &> /dev/null; then
            write_log ""Installing build-essential, pkg-config, and X11 dev libraries (for Debian/Ubuntu)...""
            sudo apt-get update >> ""$LOG_FILE"" 2>&1
            sudo apt-get install -y build-essential pkg-config libx11-dev libxi-dev libxext-dev >> ""$LOG_FILE"" 2>&1
        elif command -v dnf &> /dev/null; then
            write_log ""Installing build tools and X11 dev libraries (for Fedora/CentOS)...""
            sudo dnf groupinstall -y ""Development Tools"" >> ""$LOG_FILE"" 2>&1
            sudo dnf install -y pkg-config libX11-devel libXi-devel libXext-devel >> ""$LOG_FILE"" 2>&1
        fi
    fi
}

# --- 3. Main Script Logic ---
# Redirect all subsequent output to the log file for detailed debugging
exec > >(tee -a ""$LOG_FILE"") 2>&1

write_log ""--- Starting mindcraft-ce Installation Script ---""

install_build_deps

INSTALLATION_DIR=""$APP_LOCAL_FOLDER/mindcraft-ce-install""
BACKUP_DIR=""/tmp/mindcraft-ce-backup""

# --- 4. Backup Existing Installation ---
if [ -d ""$INSTALLATION_DIR"" ]; then
    write_log ""Previous installation found. Backing up user data...""
    rm -rf ""$BACKUP_DIR"" # Clean up from any previous failed run
    mkdir -p ""$BACKUP_DIR""
    
    # Backup files and folders if they exist
    [ -f ""$INSTALLATION_DIR/settings.json"" ] && mv ""$INSTALLATION_DIR/settings.json"" ""$BACKUP_DIR/"" && write_log ""... Backing up settings.json""
    [ -f ""$INSTALLATION_DIR/keys.json"" ] && mv ""$INSTALLATION_DIR/keys.json"" ""$BACKUP_DIR/"" && write_log ""... Backing up keys.json""
    [ -f ""$INSTALLATION_DIR/.logging_consent"" ] && mv ""$INSTALLATION_DIR/.logging_consent"" ""$BACKUP_DIR/"" && write_log ""... Backing up .logging_consent""
    [ -d ""$INSTALLATION_DIR/gui_agents"" ] && mv ""$INSTALLATION_DIR/gui_agents"" ""$BACKUP_DIR/"" && write_log ""... Backing up gui_agents/*""
    [ -d ""$INSTALLATION_DIR/bots"" ] && mv ""$INSTALLATION_DIR/bots"" ""$BACKUP_DIR/"" && write_log ""... Backing up bots/*""

    write_log ""Backup complete. Removing old installation...""
    rm -rf ""$INSTALLATION_DIR""
fi

# --- 5. Install Dependencies ---
# Note: On Linux/macOS, we don't need to add to PATH. The package managers handle it.
install_dependency ""git"" ""git""
install_dependency ""node"" ""nodejs"" # On some systems, 'node' comes from the 'nodejs' package

# --- 6. Download and Extract Project ---
write_log ""--- Downloading and Extracting mindcraft-ce ---""
ZIP_URL=""https://github.com/mindcraft-ce/mindcraft-ce/archive/refs/tags/$LATEST_VERSION.zip""
ZIP_FILE=""/tmp/mindcraft-ce-$LATEST_VERSION.zip""
TEMP_EXTRACT_DIR=""/tmp/mindcraft-ce-extract""

rm -rf ""$INSTALLATION_DIR"" ""$TEMP_EXTRACT_DIR""
mkdir -p ""$INSTALLATION_DIR""

write_log ""Downloading from $ZIP_URL...""
# Use curl with -L to follow redirects and -o to specify output file
curl -L ""$ZIP_URL"" -o ""$ZIP_FILE""

write_log ""Extracting archive...""
unzip -q ""$ZIP_FILE"" -d ""$TEMP_EXTRACT_DIR""
# Move contents from the nested directory (e.g., mindcraft-ce-1.0.0/*) to the final destination
mv ""$TEMP_EXTRACT_DIR""/*/* ""$INSTALLATION_DIR""
rm -rf ""$TEMP_EXTRACT_DIR"" ""$ZIP_FILE""
cd ""$INSTALLATION_DIR""

# --- 7. Restore User Data ---
if [ -d ""$BACKUP_DIR"" ]; then
    write_log ""--- Restoring User Data ---""
    # Use cp and then rm to avoid issues with moving into a non-empty directory
    cp -r ""$BACKUP_DIR""/* ""$INSTALLATION_DIR""/
    write_log ""User data restored.""
    rm -rf ""$BACKUP_DIR""
fi

INSTALLATION_DIR=""$APP_LOCAL_FOLDER/mindcraft-ce-install""

ORIGINAL_USER=$(logname)
if [ -z ""$ORIGINAL_USER"" ]; then
    write_log ""FATAL: Could not determine the original user.""
    exit 1
fi

# 4. Create the User-Specific Script in /tmp
USER_SCRIPT_PATH=""/tmp/mindcraft_user_setup.sh""
write_log ""Creating user-specific setup script at $USER_SCRIPT_PATH""

rm -f ""$USER_SCRIPT_PATH""

# Using a heredoc to write the user script to a file. This is safe and robust.
cat <<EOF > ""$USER_SCRIPT_PATH""
#!/bin/bash
set -e

# Redirect all output from this user script to the main log file
exec > >(tee -a ""$LOG_FILE"") 2>&1

echo ""--- Starting User-Specific Setup (as \$USER) ---""
cd ""$INSTALLATION_DIR""

# Configure Project Files (if it's a fresh install)
if [ ! -f ""settings.json"" ]; then
    echo ""Fresh install detected. Generating default configuration...""
    mv keys.example.json keys.json
    sed -i.bak 's/""\.\/andy\.json"",/""\.\/gui_agents\/andy\.json"",/g' settings.js
    mkdir -p gui_agents
    mv andy.json gui_agents/
    node -e ""(async () => { const fs = require('fs'); const settings = await import('./settings.js'); fs.writeFileSync('./settings.json', JSON.stringify(settings.default, null, 2)); })()""
    printf '{\n  ""consent"": false\n}' > .logging_consent
fi

# Run npm install as the correct user with their Node version

export NVM_DIR=""\$HOME/.nvm""
if [ -s ""\$NVM_DIR/nvm.sh"" ]; then
    \. ""\$NVM_DIR/nvm.sh""
    echo ""NVM sourced successfully.""
else
    echo ""NVM script not found, proceeding with system node.""
fi

echo ""node --version""
node --version

echo ""--- Running npm install ---""
npm install --verbose
EOF

# 5. Set Permissions and Ownership for the new script AND the installation dir
write_log ""Setting permissions and ownership...""
chmod +x ""$USER_SCRIPT_PATH""
chown ""$ORIGINAL_USER"" ""$USER_SCRIPT_PATH""
chown -R ""$ORIGINAL_USER"" ""$INSTALLATION_DIR""

# 6. Execute the User Script as the correct user
write_log ""Executing setup script as '$ORIGINAL_USER'...""
su - ""$ORIGINAL_USER"" -c ""$USER_SCRIPT_PATH""

# 7. Clean up the temporary script
rm ""$USER_SCRIPT_PATH""


# --- 10. Finalize and Update Metadata ---
write_log ""--- Finalizing installation ---""
META_FILE_PATH=""$APP_LOCAL_FOLDER/metadata.json""
# Use printf to safely create the JSON content
printf '{""version"":""%s"",""installed"":true,""installation_path"":""%s""}' ""$LATEST_VERSION"" ""$INSTALLATION_DIR"" > ""$META_FILE_PATH""

# --- 11. chown dir ---
ORIGINAL_USER=$(logname)
if [ -n ""$ORIGINAL_USER"" ]; then
    write_log ""Changing ownership of '$INSTALLATION_DIR' to $ORIGINAL_USER...""
    chown -R ""$ORIGINAL_USER"" ""$INSTALLATION_DIR""
fi

write_log ""--- mindcraft-ce has been successfully installed! ---""
exit 0";
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