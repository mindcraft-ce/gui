using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Input;
using MindcraftCE.Models;
using MindcraftCE.Views;
using Newtonsoft.Json.Linq;
using Windows.Storage;

namespace MindcraftCE.ViewModels
{
    public class AgentViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Agent> Agents { get; } = new();

        private Agent _selectedAgent;
        public Agent SelectedAgent
        {
            get => _selectedAgent;
            set
            {
                _selectedAgent = value;
                OnPropertyChanged();
            }
        }

        public AgentViewModel()
        {
            JObject metadata = UpdatesView.GetMetadataSync();
            if (metadata["installed"]?.Value<bool>() == true)
            {
                var settings = UpdatesView.GetSettingsSync();
                // Convert each string path in enabledAgents to a Full Path
                var enabledAgents = settings["profiles"]?.Values<string>().Select(Path.GetFileName).ToArray();
                var installationPath = metadata["installation_path"]?.Value<string>();
                var agentsPath = Path.Combine(installationPath, "gui_agents");
                // List all the agents from the agentsPath folder
                var agentFiles = Directory.GetFiles(agentsPath, "*.json");

                foreach (var file in agentFiles)
                {
                    try
                    {
                        JObject agentJson = JObject.Parse(File.ReadAllText(file));
                        var model = agentJson["model"];
                        agentJson["model"] = null; // Remove the model property to avoid deserialization issues
                        Agent agent = agentJson.ToObject<Agent>();
                        // Deserialize the model separately
                        if (model != null)
                        {
                            // Check if the model is a string or an object
                            if (model.Type == JTokenType.String)
                            {
                                agent.ModelInfo = new Model(model.Value<string>());
                            }
                            else if (model.Type == JTokenType.Object)
                            {
                                agent.ModelInfo = new Model(model.Value<JObject>());
                            }
                            else
                            {
                                agent.ModelInfo = new Model("pollinations/openai");
                            }
                        }
                        else
                        {
                            agent.ModelInfo = new Model("pollinations/openai");
                        }
                        agent.FileName = Path.GetFileName(file);
                        bool enabled = enabledAgents?.Contains(agent.FileName) == true;
                        agent.IsChecked = enabled;
                        // modes
                        agent.Modes = agentJson["modes"]?.ToObject<Modes>() ?? new Modes();
                        Agents.Add(agent);
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions, e.g., log them or show a message to the user
                        Console.WriteLine($"Error loading agent from {file}: {ex.Message}");
                    }
                }
            }
        }

        public string GetAgentPath(Agent agent)
        {
            JObject metadata = UpdatesView.GetMetadataSync();
            if (metadata["installed"]?.Value<bool>() == true)
            {
                var installationPath = metadata["installation_path"]?.Value<string>();
                return Path.Combine(installationPath, "gui_agents", agent.FileName);
            }
            return string.Empty;
        }

        public async Task RemoveAgent(Agent agent)
        {
            if (agent == null || !Agents.Contains(agent)) return;

            try
            {
                // 1. Remove from the in-memory collection first, so the UI updates instantly
                Agents.Remove(agent);

                var metadata = await UpdatesView.GetMetadata();
                var installationPath = metadata["installation_path"]?.Value<string>();
                if (string.IsNullOrEmpty(installationPath)) return;

                // 2. Delete the agent's individual .json file
                var agentFilePath = GetAgentPath(agent);
                if (File.Exists(agentFilePath))
                {
                    File.Delete(agentFilePath);
                }

                // 3. Update settings.json to remove the agent
                var settingsPath = Path.Combine(installationPath, "settings.json");
                var settings = JObject.Parse(await File.ReadAllTextAsync(settingsPath));

                var profilesArray = settings["profiles"] as JArray;
                if (profilesArray != null)
                {
                    string agentProfilePath = "./gui_agents/" + agent.FileName;
                    var tokenToRemove = profilesArray.FirstOrDefault(t => t.Value<string>() == agentProfilePath);

                    if (tokenToRemove != null)
                    {
                        tokenToRemove.Remove();
                        await File.WriteAllTextAsync(settingsPath, settings.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing agent: {ex.Message}");
                // Add the agent back to the list on failure to keep UI consistent
                if (!Agents.Contains(agent)) Agents.Add(agent);
            }
        }

        public async Task<Agent?> CreateAgent(string fileName)
        {
            try
            {
                var metadata = await UpdatesView.GetMetadata();
                var installationPath = metadata["installation_path"]?.Value<string>();
                if (string.IsNullOrEmpty(installationPath)) return null;

                string agentPath = Path.Combine(installationPath, "gui_agents", fileName);

                // Check for existence
                if (Agents.Any(a => a.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)) || File.Exists(agentPath))
                {
                    return null; // Agent already exists
                }

                // 1. Create the new agent's JSON by loading the default template
                var defaultAgentPath = Path.Combine(installationPath, "profiles", "defaults", "_default.json");
                var agentJson = JObject.Parse(await File.ReadAllTextAsync(defaultAgentPath));

                // 2. Customize the JSON for the new agent
                agentJson["name"] = Path.GetFileNameWithoutExtension(fileName);

                // 3. Save the new agent file
                await File.WriteAllTextAsync(agentPath, agentJson.ToString());

                // 4. Deserialize the complete object once to get a proper Agent instance
                Agent newAgent = agentJson.ToObject<Agent>();
                newAgent.FileName = fileName;
                newAgent.IsChecked = true; // New agents should be enabled by default

                // 5. Add the agent to our in-memory collection so the UI updates
                Agents.Add(newAgent);

                // --- 6. NEW LOGIC: Update settings.json to enable the new agent ---
                var settingsPath = Path.Combine(installationPath, "settings.json");
                var settings = JObject.Parse(await File.ReadAllTextAsync(settingsPath));
                var profilesArray = settings["profiles"] as JArray ?? new JArray();

                string agentProfilePath = "./gui_agents/" + newAgent.FileName;

                // Ensure it's not already there for some reason, then add it
                if (!profilesArray.Any(t => t.Value<string>() == agentProfilePath))
                {
                    profilesArray.Add(agentProfilePath);
                }

                settings["profiles"] = profilesArray;
                await File.WriteAllTextAsync(settingsPath, settings.ToString());

                return newAgent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating agent: {ex.Message}");
                return null;
            }
        }

        public async Task EditAgent(Agent agent)
        {
            try
            {
                // Console.WriteLine($"Editing agent: {agent.FileName}");
                if (agent == null) return;

                // --- 1. Save the agent's individual file ---
                JObject agentJson = JObject.FromObject(agent);
                agentJson.Remove("FileName");
                agentJson.Remove("IsChecked"); // is_checked is handled by JsonProperty attribute if needed

                var metadata = await UpdatesView.GetMetadata();
                var installationPath = metadata["installation_path"]?.Value<string>();
                if (string.IsNullOrEmpty(installationPath)) return;

                // Load the default agent template for merging
                var defaultAgentPath = Path.Combine(installationPath, "profiles", "defaults", "_default.json");
                var defaultJson = JObject.Parse(await File.ReadAllTextAsync(defaultAgentPath));

                // Merge defaults for any null properties
                foreach (var property in defaultJson.Properties())
                {
                    if (!agentJson.ContainsKey(property.Name) || agentJson[property.Name]?.Type == JTokenType.Null)
                    {
                        agentJson[property.Name] = property.Value;
                    }
                }

                var agentFilePath = GetAgentPath(agent);
                if (string.IsNullOrEmpty(agentFilePath)) return;

                // Use async file I/O
                await File.WriteAllTextAsync(agentFilePath, agentJson.ToString());

                // --- 2. Update the main settings.json with the agent's status ---
                var settingsPath = Path.Combine(installationPath, "settings.json");
                var settings = JObject.Parse(await File.ReadAllTextAsync(settingsPath));

                // Correctly get the JArray
                var profilesArray = settings["profiles"] as JArray;
                if (profilesArray == null)
                {
                    // If for some reason "profiles" doesn't exist, create it
                    profilesArray = new JArray();
                    settings["profiles"] = profilesArray;
                }

                string agentProfilePath = "./gui_agents/" + agent.FileName;

                // Find the existing token for this agent, if any
                var existingAgentToken = profilesArray.FirstOrDefault(t => t.Value<string>() == agentProfilePath);

                if (agent.IsChecked)
                {
                    // If agent is checked AND it's not already in the list, add it.
                    if (existingAgentToken == null)
                    {
                        profilesArray.Add(agentProfilePath);
                    }
                }
                else
                {
                    // If agent is not checked AND it is in the list, remove it.
                    if (existingAgentToken != null)
                    {
                        existingAgentToken.Remove();
                    }
                }

                // Save the modified settings file
                await File.WriteAllTextAsync(settingsPath, settings.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error editing agent: {ex.Message}");
                // Optionally show a dialog to the user here
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
