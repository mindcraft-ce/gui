using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using MindcraftCE.Models;
using MindcraftCE.ViewModels;
using Newtonsoft.Json.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MindcraftCE.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public sealed partial class AgentView : Page, INotifyPropertyChanged
    {
        public AgentViewModel ViewModel { get; } = new();

        public AgentView()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // If we are navigated to with an `Agent` parameter, populate the details
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (UpdatesView.GetMetadataSync()["installed"]?.Value<bool>() != true)
            {
                messageBox.Visibility = Visibility.Visible;
                messageBox.Text = "mindcraft-ce is not installed.";
                AgentListGrid.Visibility = Visibility.Collapsed;
            }
            if (e.Parameter is Agent agent)
            {
                ViewModel.SelectedAgent = agent;
                PopulateAgentDetails(agent);
            }
        }

        private async void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedAgent))
            {
                // Update UI
                PopulateAgentDetails(ViewModel.SelectedAgent);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void PopulateAgentDetails(Agent agent)
        {
            try
            {
                if (agent == null)
                { detailPanel.Children.Clear(); return; }

                detailPanel.Opacity = 0; // insta hide for "fade out"

                detailPanel.Children.Clear();

                var elementsToAnimate = new List<UIElement>();

                TextBlock selectedAgentTextBlock = new TextBlock
                {
                    Text = "Selected Agent",
                    FontSize = 24,
                    Margin = new Thickness(0, 0, 0, 20),
                };

                detailPanel.Children.Add(selectedAgentTextBlock);

                string buttonsXaml = @"
    <StackPanel xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                Orientation='Horizontal'>

        <Button x:Name='SaveButton' Margin='5,0,5,0'>
            <StackPanel Orientation='Horizontal'>
                <FontIcon Glyph='&#xE74E;'/>
                <TextBlock Text='Save' Margin='5,0,0,0' VerticalAlignment='Center'/>
            </StackPanel>
        </Button>

        <Button x:Name='DeleteButton' Margin='5,0,5,0' Style='{StaticResource DestructiveButtonStyle}'>
            <StackPanel Orientation='Horizontal'>
                <FontIcon Glyph='&#xE74D;'/>
                <TextBlock Text='Delete' Margin='5,0,0,0' VerticalAlignment='Center'/>
            </StackPanel>
        </Button>

    </StackPanel>";

                try
                {
                    // 2. Load the XAML string into a UI element.
                    StackPanel buttonPanel = (StackPanel)Microsoft.UI.Xaml.Markup.XamlReader.Load(buttonsXaml);

                    // 3. Find the named controls within the newly created panel.
                    Button saveButton = (Button)buttonPanel.FindName("SaveButton");
                    Button deleteButton = (Button)buttonPanel.FindName("DeleteButton");

                    // 4. Wire up the event handlers in C#.
                    if (saveButton != null)
                    {
                        saveButton.Click += SaveButton_Click;
                    }
                    if (deleteButton != null)
                    {
                        deleteButton.DataContext = agent;
                        deleteButton.Click += DeleteButton_Click;
                    }

                    // 5. Add the entire button panel to your detailPanel.
                    detailPanel.Children.Add(buttonPanel);
                }
                catch (Exception ex)
                {
                    // It's always a good idea to have a catch block when using XamlReader.
                    Console.WriteLine($"Failed to load button XAML: {ex.Message}");
                }

                var properties = typeof(Agent).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                String[] PropertiesToIgnore = ["FileName"];

                foreach (var prop in properties)
                {
                    var propName = prop.Name;
                    if (PropertiesToIgnore.Contains(propName))
                        continue;
                    var propValueObj = prop.GetValue(agent);

                    // Set font sizes
                    double fontSize = 16;
                    if (propName == "Name") fontSize = 20;
                    else if (propName == "Model") fontSize = 18;

                    elementsToAnimate.Add(CreateDetailRow(prop, propValueObj, fontSize));
                }

                foreach (var el in elementsToAnimate)
                {
                    detailPanel.Children.Add(el);
                }

                // Create the fade-in storyboard with staggered delays
                var storyboard = new Storyboard();

                double delay = 0;
                double delayIncrement = 0.05;

                foreach (var el in elementsToAnimate)
                {
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(400),
                        BeginTime = TimeSpan.FromSeconds(delay),
                        EnableDependentAnimation = true
                    };

                    Storyboard.SetTarget(fadeIn, el);
                    Storyboard.SetTargetProperty(fadeIn, "Opacity");

                    storyboard.Children.Add(fadeIn);

                    // Ensure the element starts hidden
                    el.Opacity = 0;

                    delay += delayIncrement;
                }

                // Fade in the container itself too (optional)
                var containerFadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EnableDependentAnimation = true,
                };
                Storyboard.SetTarget(containerFadeIn, detailPanel);
                Storyboard.SetTargetProperty(containerFadeIn, "Opacity");
                storyboard.Children.Add(containerFadeIn);

                storyboard.Begin();
            } catch (Exception ex)
            {
                Console.WriteLine($"Error populating agent details: {ex}");
            }
        }

        private FrameworkElement FindControlForProperty(IEnumerable<Grid> rows, string propertyName)
        {
            foreach (var row in rows)
            {
                // Find the input control, which is the second element in the row's Grid.
                // It could be a TextBox, NumberBox, or another Grid.
                var control = row.Children.OfType<FrameworkElement>().Skip(1).FirstOrDefault();

                // Check if its Tag matches our property name. This works for simple controls
                // and for the container grids of complex types.
                if (control?.Tag as string == propertyName)
                {
                    return control;
                }
            }
            return null;
        }

        private Agent GetAgentDetails()
        {
            try
            {
                var agent = new Agent
                {
                    FileName = ViewModel.SelectedAgent?.FileName,
                    ModelInfo = new Model(), // Ensure ModelInfo is never null
                    Modes = new Modes(),     // Ensure Modes is never null
                };

                // This gets all the "row" Grids we created
                var detailRows = detailPanel.Children.OfType<Grid>();

                foreach (var prop in typeof(Agent).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    // Find the specific UI control for this property
                    var control = FindControlForProperty(detailRows, prop.Name);
                    if (control == null) continue;

                    // --- Handle each type directly using the found control ---
                    if (control is TextBox tb)
                    {
                        prop.SetValue(agent, tb.Text);
                    }
                    else if (control is NumberBox nb)
                    {
                        prop.SetValue(agent, Convert.ToInt32(nb.Value));
                    }
                    else if (control is ToggleSwitch ts)
                    {
                        prop.SetValue(agent, ts.IsOn);
                    }
                    else if (control is Grid grid && prop.PropertyType == typeof(Model))
                    {
                        // var model = new Model();
                        foreach (var modelProp in typeof(Model).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            // Find the TextBox whose Tag matches the property's name
                            var innerTextBox = grid.Children.OfType<TextBox>()
                                                   .FirstOrDefault(tb => tb.Tag as string == modelProp.Name);

                            if (innerTextBox != null && modelProp.CanWrite)
                            {
                                modelProp.SetValue(agent.ModelInfo, innerTextBox.Text);
                            }
                        }
                        // prop.SetValue(agent, model);
                    }
                    else if (control is Grid _grid && prop.PropertyType == typeof(Modes))
                    {
                        // var modes = new Modes();

                        var toggleSwitchesInGrid = _grid.Children.OfType<ToggleSwitch>();

                        foreach (var modeToggle in toggleSwitchesInGrid)
                        {
                            // Get the property name from the Tag we set in CreateDetailRow
                            var modeName = modeToggle.Tag as string;
                            if (string.IsNullOrEmpty(modeName)) continue;

                            // Get the corresponding property from the Modes class (e.g., "SelfDefense")
                            var modePropInfo = typeof(Modes).GetProperty(modeName);

                            if (modePropInfo != null && modePropInfo.CanWrite)
                            {
                                modePropInfo.SetValue(agent.Modes, modeToggle.IsOn);
                            }
                        }

                        // prop.SetValue(agent, modes);
                    }
                }
                return agent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting agent details: {ex}");
                throw new InvalidOperationException("Failed to retrieve agent details.", ex);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Agent updatedAgent = GetAgentDetails();
                var oldAgent = ViewModel.Agents.FirstOrDefault(a => a.FileName == updatedAgent.FileName);
                if (oldAgent != null)
                {
                    int index = ViewModel.Agents.IndexOf(oldAgent);

                    // 3. Replace the old agent with the new one at that index
                    // This will correctly notify the ListView to update the item.
                    ViewModel.Agents[index] = updatedAgent;
                }

                ViewModel.SelectedAgent = updatedAgent;
                ViewModel.EditAgent(updatedAgent);

                ContentDialog saveDialog = new ContentDialog
                {
                    Title = "Agent Saved",
                    Content = $"The agent '{updatedAgent.Name}' has been saved successfully.",
                    CloseButtonText = "OK",
                };
                saveDialog.ShowAsync();
            } catch (Exception ex)
            {
                Console.WriteLine($"Error saving agent: {ex}");
                new ContentDialog
                {
                    Title = "Error Saving Agent",
                    Content = $"An error occurred while saving the agent: {ex.Message}",
                    CloseButtonText = "OK",
                }.ShowAsync();
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Agent agent = (sender as Button)?.DataContext as Agent;
            if (agent == null) return;

            ContentDialog deleteDialog = new ContentDialog
            {
                Title = "Delete Agent",
                Content = $"Are you sure you want to delete the agent '{agent.Name}'?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                PrimaryButtonStyle = (Style)Application.Current.Resources["DestructiveButtonStyle"],
            };

            var result = await deleteDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.RemoveAgent(agent);
            }
        }

        private Grid CreateDetailRow(PropertyInfo prop, object valueObj, double fontSize)
        {
            var panel = new Grid { Margin = new Thickness(0, 0, 0, 8) };

            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // string value = valueObj != null ? valueObj.ToString() : "Not Set";
            string propName = prop.Name;
            if (propName == "IsChecked")
            {
                propName = "Enabled";
            }

            var labelText = new TextBlock
            {
                Text = CamelCaseToNormal(propName) + ": ",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = fontSize,
            };
            Grid.SetColumn(labelText, 0);

            FrameworkElement valueElement = null;
            Type type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if      (type == typeof(string))
            {
                valueElement = new TextBox
                {
                    Text = valueObj?.ToString() ?? "",
                    PlaceholderText = "Default",
                    FontSize = fontSize,
                    Tag = prop.Name,
                };
            }
            else if (type == typeof(int))
            {
                valueElement = new NumberBox
                {
                    Value = valueObj != null ? Convert.ToDouble(valueObj) : 0,
                    PlaceholderText = "Default",
                    FontSize = fontSize,
                    Tag = prop.Name,
                };
            }
            else if (type == typeof(bool))
            {
                valueElement = new ToggleSwitch
                {
                    IsOn = valueObj != null && (bool)valueObj,
                    FontSize = fontSize,
                    Tag = prop.Name,
                };
            }
            else if (type == typeof(Model))
            {
                var model = valueObj as Model;
                var grid = new Grid
                {
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4),
                    // Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)),
                    // ^^^ removed to fix dark mode
                    Tag = prop.Name // Tag for the container grid is still "ModelInfo"
                };

                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int i = 0;
                // --- START OF FIX: Loop through Model's properties ---
                foreach (var modelProp in typeof(Model).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    string label = CamelCaseToNormal(modelProp.Name);
                    string value = modelProp.GetValue(model)?.ToString();

                    var labelText_ = new TextBlock
                    {
                        Text = label + ":",
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = fontSize,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                    };
                    Grid.SetRow(labelText_, i);
                    Grid.SetColumn(labelText_, 0);
                    grid.Children.Add(labelText_);

                    var textBox = new TextBox
                    {
                        Text = value ?? "",
                        PlaceholderText = "Auto-Detect",
                        FontSize = fontSize,
                        // Use the actual property name as the Tag! e.g. "ModelName", "Api"
                        Tag = modelProp.Name
                    };
                    Grid.SetRow(textBox, i);
                    Grid.SetColumn(textBox, 1);
                    grid.Children.Add(textBox);
                    i++;
                }
                valueElement = grid;
            }
            else if (type == typeof(Modes))
            {
                var modes = valueObj as Modes;
                var grid = new Grid
                {
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4),
                    // Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)),
                    // ^^^ removed to fix dark mode
                    Tag = prop.Name
                };

                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // For the Toggle
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // For the Label

                int i = 0;
                foreach (var modeProp in typeof(Modes).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    bool isEnabled = modeProp.GetValue(modes) is bool b && b;

                    var toggleSwitch = new ToggleSwitch
                    {
                        IsOn = isEnabled,
                        Tag = modeProp.Name,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetRow(toggleSwitch, i);
                    Grid.SetColumn(toggleSwitch, 0); // Place in first column
                    grid.Children.Add(toggleSwitch);

                    var labelText_ = new TextBlock
                    {
                        Text = CamelCaseToNormal(modeProp.Name),
                        FontSize = fontSize,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    Grid.SetRow(labelText_, i);
                    Grid.SetColumn(labelText_, 1); // Place in second column
                    grid.Children.Add(labelText_);

                    i++;
                }
                valueElement = grid;
            }
            else // Uneditable
            {
                valueElement = new TextBlock
                {
                    Text =  "Default",
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128))
                };
            }



            Grid.SetColumn(valueElement, 1);

            panel.Children.Add(labelText);
            panel.Children.Add(valueElement);

            return panel;
        }

        public static string CamelCaseToNormal(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var result = new System.Text.StringBuilder();
            result.Append(input[0]);

            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]) && !char.IsWhiteSpace(input[i - 1]))
                {
                    result.Append(' ');
                }
                result.Append(input[i]);
            }

            return result.ToString();
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Create the UI controls for the dialog
            var inputTextBox = new TextBox
            {
                PlaceholderText = "New agent name",
                Margin = new Thickness(0, 0, 8, 0),
                Width = 200 // Give it a specific width
            };

            var busyRing = new ProgressRing
            {
                IsActive = false, // Initially inactive
                Visibility = Visibility.Collapsed, // Initially hidden
                Width = 20,
                Height = 20,
                Margin = new Thickness(8, 0, 0, 0)
            };

            var validationMessage = new TextBlock
            {
                Text = "A file with this name already exists.",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var contentPanel = new StackPanel();
            var inputPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
        {
            inputTextBox,
            new TextBlock { Text = ".json", VerticalAlignment = VerticalAlignment.Center },
            busyRing // Add the ProgressRing next to the input
        }
            };
            contentPanel.Children.Add(inputPanel);
            contentPanel.Children.Add(validationMessage);

            // 2. Create the ContentDialog
            var createDialog = new ContentDialog
            {
                Title = "Create New Agent",
                Content = contentPanel,
                PrimaryButtonText = "Create",
                CloseButtonText = "Cancel",
                IsPrimaryButtonEnabled = false
            };

            // 3. Add real-time validation logic
            inputTextBox.TextChanged += (s, args) =>
            {
                bool isValid = IsFileNameValid(inputTextBox.Text, out string errorMessage);
                validationMessage.Text = errorMessage;
                validationMessage.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;
                createDialog.IsPrimaryButtonEnabled = isValid;
            };

            // 4. Show the dialog and wait for the user's action
            var result = await createDialog.ShowAsync();

            // 5. If the user clicked "Create", proceed with creation
            if (result == ContentDialogResult.Primary)
            {
                // --- THIS IS THE NEW "BUSY" STATE LOGIC ---
                // Disable all inputs and show the progress ring
                inputTextBox.IsEnabled = false;
                createDialog.IsPrimaryButtonEnabled = false;
                createDialog.CloseButtonText = ""; // Hide the cancel button so the user can't close it
                busyRing.IsActive = true;
                busyRing.Visibility = Visibility.Visible;
                // --- END OF NEW LOGIC ---

                string finalFileName = inputTextBox.Text + ".json";
                Agent? newAgent = null;

                try
                {
                    // Call the ViewModel's method
                    newAgent = await ViewModel.CreateAgent(finalFileName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during agent creation: {ex.Message}");
                    // The error dialog will be shown in the finally block
                }
                finally
                {
                    // Hide the dialog regardless of success or failure
                    createDialog.Hide();
                }

                if (newAgent != null)
                {
                    ViewModel.SelectedAgent = newAgent;
                    agentListView.ScrollIntoView(newAgent);
                }
                else
                {
                    // Show an error if creation failed
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to create the agent '{finalFileName}'.",
                        CloseButtonText = "OK",
                        
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }


        // Helper method for validation logic
        private bool IsFileNameValid(string name, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(name))
            {
                // Don't show an error for an empty box, just keep the button disabled.
                return false;
            }

            // Check for invalid characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (name.Any(c => invalidChars.Contains(c)))
            {
                errorMessage = "Name contains invalid characters.";
                return false;
            }

            // Check if the agent already exists (using the ViewModel's data)
            string finalFileName = name + ".json";
            if (ViewModel.Agents.Any(a => a.FileName.Equals(finalFileName, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = "An agent with this name already exists.";
                return false;
            }

            // All checks passed
            return true;
        }
    }
}
