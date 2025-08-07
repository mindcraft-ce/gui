using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using mindcraft_ce.Models;
using Newtonsoft.Json.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace mindcraft_ce.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsView : Page
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateSettings();
        }

        private Settings _originalSettings;
        private readonly List<string> _settingsToIgnore = new() { "Profiles" };
        private string _settingsFilePath = string.Empty;

        private async void UpdateSettings()
        {
            SettingsPanel.Children.Add(new ProgressRing { IsActive = true });
            saveButton.Visibility = Visibility.Collapsed;
            try
            {
                var installationPath = (await UpdatesView.GetMetadata())["installation_path"]?.Value<String>();
                _settingsFilePath = Path.Combine(installationPath, "settings.json");
                _originalSettings = JObject.Parse(await FileIO.ReadTextAsync(await StorageFile.GetFileFromPathAsync(_settingsFilePath))).ToObject<Settings>();
                SettingsPanel.Children.Clear();
                saveButton.Visibility = Visibility.Visible;
                double fontSize = 16;

                var basic_settings = new List<string> { "MinecraftVersion", "Host", "Port", "Auth", "OnlyChatWith" };
                var dropdownElements = new Dictionary<string, List<string>>
                {
                    { "MinecraftVersion", new List<string>
                        {
                            "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21.0",
                            "1.20.6", "1.20.5", "1.20.4", "1.20.3", "1.20.2", "1.20.1", "1.20.0",
                            "1.19.4", "1.19.3", "1.19.2", "1.19.1", "1.19.0",
                            "1.18.2", "1.18.1", "1.18.0",
                            "1.17.1", "1.17.0",
                            "1.16.5", "1.16.4", "1.16.3", "1.16.2", "1.16.1", "1.16.0",
                            "1.15.2", "1.15.1", "1.15.0",
                            "1.14.4", "1.14.3", "1.14.2", "1.14.1", "1.14.0",
                            "1.13.2", "1.13.1", "1.13.0",
                            "1.12.2", "1.12.1", "1.12.0",
                            "1.11.2", "1.11.1", "1.11.0",
                            "1.10.2", "1.10.1", "1.10.0",
                            "1.9.4", "1.9.3", "1.9.2", "1.9.1", "1.9.0",
                            "1.8.9", "1.8.8", "1.8.7", "1.8.6", "1.8.5", "1.8.4", "1.8.3", "1.8.2", "1.8.1", "1.8.0"
                        }
                    },
                    { "Auth", new List<string> { "offline", "microsoft" } },
                    { "VisionMode", new List<string> { "off", "prompted", "always" } },
                };


                var properties = _originalSettings.GetType().GetProperties();
                var basic = new List<PropertyInfo>();
                var advanced = new List<PropertyInfo>();

                foreach (PropertyInfo prop in properties)
                {
                    if (_settingsToIgnore.Contains(prop.Name)) continue;
                    if (basic_settings.Contains(prop.Name)) { basic.Add(prop); } else { advanced.Add(prop); }
                };

                SettingsPanel.Children.Add(new TextBlock { FontSize = 32, Margin = new Thickness(0, 0, 0, 10), Text = "Basic" });
                foreach (PropertyInfo prop in basic)
                {
                    object valueObj = prop.GetValue(_originalSettings);
                    if (valueObj == null) continue; // Skip null values
                    var detailRow = CreateDetailRow(prop, valueObj, fontSize, dropdownElements);
                    SettingsPanel.Children.Add(detailRow);
                }

                SettingsPanel.Children.Add(new TextBlock { FontSize = 32, Margin = new Thickness(0, 0, 0, 10), Text = "Advanced" });
                foreach (PropertyInfo prop in advanced)
                {
                    object valueObj = prop.GetValue(_originalSettings);
                    if (valueObj == null) continue; // Skip null values
                    var detailRow = CreateDetailRow(prop, valueObj, fontSize, dropdownElements);
                    SettingsPanel.Children.Add(detailRow);
                }


            } catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Failed to load settings: " + ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot,
                };
                await errorDialog.ShowAsync();
            }
        }

        private Grid CreateDetailRow(PropertyInfo prop, object valueObj, double fontSize, Dictionary<string, List<string>> dropdownElements)
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
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            Grid.SetColumn(labelText, 0);

            FrameworkElement valueElement = null;
            Type type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (dropdownElements.ContainsKey(propName))
            {
                valueElement = new ComboBox
                {
                    FontSize = fontSize,
                    ItemsSource = dropdownElements[propName],
                    SelectedItem = valueObj?.ToString() ?? dropdownElements[propName].FirstOrDefault(),
                };
            }
            else
            if (type == typeof(string))
            {
                valueElement = new TextBox
                {
                    Text = valueObj?.ToString() ?? "",
                    PlaceholderText = "Default",
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                    IsReadOnly = false
                };
            }
            else if (type == typeof(int) || type == typeof(float) || type == typeof(double))
            {
                valueElement = new NumberBox
                {
                    Value = valueObj != null ? Convert.ToDouble(valueObj) : 0,
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                };
            }
            else if (type == typeof(bool))
            {
                valueElement = new ToggleSwitch
                {
                    IsOn = valueObj != null && (bool)valueObj,
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                };
            }
            else if (type == typeof(List<string>))
            {
                var list = valueObj as List<string> ?? new List<string>();
                var grid = new Grid
                {
                    // Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)),
                    // ^^^ removed to fix dark mode
                    Padding = new Thickness(6),
                    CornerRadius = new CornerRadius(4),
                };

                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                void AddListItemRow(Grid grid, string itemText, double fontSize, int rowIndex)
                {
                    var newRowDefinition = new RowDefinition { Height = GridLength.Auto };
                    grid.RowDefinitions.Insert(rowIndex, newRowDefinition);

                    var itemTextBox = new TextBox
                    {
                        Text = itemText,
                        FontSize = fontSize,
                        Margin = new Thickness(0, 2, 0, 2),
                    };

                    var deleteButton = new Button
                    {
                        Content = new FontIcon { Glyph = "\uE74D" }, // Delete icon
                        FontSize = fontSize,
                        Margin = new Thickness(8, 0, 0, 0),
                    };

                    deleteButton.Click += (s, e) =>
                    {
                        // This simple removal is fine, though it leaves an empty row definition.
                        // For this UI, it is visually acceptable and much simpler than re-indexing all rows on delete.
                        grid.Children.Remove(itemTextBox);
                        grid.Children.Remove(deleteButton);
                    };

                    // Set Column/Row for the new controls before adding them
                    Grid.SetColumn(itemTextBox, 0);
                    Grid.SetRow(itemTextBox, rowIndex);
                    Grid.SetColumn(deleteButton, 1);
                    Grid.SetRow(deleteButton, rowIndex);

                    // Add the controls to the Grid's children collection
                    grid.Children.Add(itemTextBox);
                    grid.Children.Add(deleteButton);

                    // --- FIX IS HERE ---
                    // After inserting a new row, we must shift down any controls that came after it.
                    // Use OfType<FrameworkElement>() to safely get only the elements we can work with.
                    foreach (var feChild in grid.Children.OfType<FrameworkElement>())
                    {
                        // Don't shift the controls we just added
                        if (feChild == itemTextBox || feChild == deleteButton) continue;

                        int currentRow = Grid.GetRow(feChild);
                        if (currentRow >= rowIndex)
                        {
                            // Now this works, because feChild is a FrameworkElement
                            Grid.SetRow(feChild, currentRow + 1);
                        }
                    }
                }

                // Add initial items from the list
                foreach (string item in list)
                {
                    AddListItemRow(grid, item, fontSize, grid.RowDefinitions.Count);
                }

                // Create and add the 'Add New Item' controls
                var addNewItemBox = new TextBox
                {
                    PlaceholderText = "Add new item",
                    FontSize = fontSize,
                    Margin = new Thickness(0, 2, 0, 2)
                };

                var addButton = new Button
                {
                    Content = new FontIcon { Glyph = "\uE710" }, // Add icon
                    FontSize = fontSize,
                    Margin = new Thickness(8, 0, 0, 0)
                };

                // Add a new row definition and place the controls in it
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(addNewItemBox, grid.RowDefinitions.Count - 1);
                Grid.SetColumn(addNewItemBox, 0);
                Grid.SetRow(addButton, grid.RowDefinitions.Count - 1);
                Grid.SetColumn(addButton, 1);

                grid.Children.Add(addNewItemBox);
                grid.Children.Add(addButton);

                // Wire up the Add button's click event
                addButton.Click += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(addNewItemBox.Text))
                    {
                        // Insert the new item row *before* the add button's row
                        AddListItemRow(grid, addNewItemBox.Text, fontSize, grid.RowDefinitions.Count - 1);
                        addNewItemBox.Text = ""; // Clear the input
                    }
                };

                valueElement = grid;
            }
            else if (type == typeof(AutoIdleTrigger))
            {
                var trigger = valueObj as AutoIdleTrigger;

                var grid = new Grid
                {
                    // Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)),
                    // ^^^ removed to fix dark mode
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4),
                    Tag = "AutoIdleTrigger"
                };

                // 3 rows, 2 columns
                for (int i = 0; i < 3; i++)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                void AddLabeledField(string label, object value, int row)
                {
                    var labelText = new TextBlock
                    {
                        Text = label + ":",
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = fontSize,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                    };
                    Grid.SetRow(labelText, row);
                    Grid.SetColumn(labelText, 0);
                    grid.Children.Add(labelText);

                    if (value == null)
                    {
                        Debug.WriteLine($"Skipping null value for field '{label}'");
                        return;
                    }

                    Type type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();

                    FrameworkElement element = null;
                    if (type == typeof(string))
                    {
                        element = new TextBox
                        {
                            Text = value?.ToString() ?? "",
                            PlaceholderText = "Auto-Detect",
                            FontSize = fontSize,
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                        };
                    }
                    else if (type == typeof(int))
                    {
                        element = new NumberBox
                        {
                            Value = value != null ? Convert.ToDouble(value) : 0,
                            FontSize = fontSize,
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                        };
                    }
                    else if (type == typeof(bool))
                    {
                        element = new ToggleSwitch
                        {
                            IsOn = value != null && (bool)value,
                            FontSize = fontSize,
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                        };
                    }

                    if (element != null)
                    {
                        Grid.SetRow(element, row);
                        Grid.SetColumn(element, 1);
                        grid.Children.Add(element);
                    }
                    else
                    {
                        Debug.WriteLine($"Element was null for field '{label}' with type {type}");
                    }
                }

                AddLabeledField("Enabled", trigger?.Enabled, 0);
                AddLabeledField("Timeout (s)", trigger?.TimeoutSecs, 1);
                AddLabeledField("Message", trigger?.Message, 2);

                valueElement = grid;
            }
            else // Uneditable
            {
                valueElement = new TextBlock
                {
                    Text = valueObj?.ToString() ?? "Default",
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128))
                };
            }

            valueElement.HorizontalAlignment = HorizontalAlignment.Stretch;

            Grid.SetColumn(valueElement, 1);
            valueElement.Tag = prop.Name;
            panel.Children.Add(labelText);
            panel.Children.Add(valueElement);

            return panel;
        }

        private object GetDetailRow(string name)
        {
            foreach (var grid in SettingsPanel.Children.OfType<Grid>())
            {
                var valueElement = grid.Children.OfType<FrameworkElement>().FirstOrDefault(fe => fe.Tag as string == name);
                if (valueElement is TextBox textBox)
                {
                    return textBox.Text;
                }
                else if (valueElement is ComboBox comboBox)
                {
                    return comboBox.SelectedItem.ToString();
                }
                else if (valueElement is NumberBox numberBox)
                {
                    return numberBox.Value;
                }
                else if (valueElement is ToggleSwitch toggleSwitch)
                {
                    return toggleSwitch.IsOn;
                }
                else if (valueElement is Grid grid_) {
                    // Could be either a List or a AutoIdleTrigger, check the `Tag` to find out.
                    if (grid_.Tag is string tag && tag == "AutoIdleTrigger")
                    {
                        var trigger = new AutoIdleTrigger();
                        foreach (var child in grid_.Children.OfType<FrameworkElement>())
                        {
                            if (child is TextBox textBox_)
                            {
                                if (textBox_.Text != "Auto-Detect")
                                {
                                    if (textBox_.Text == "Enabled") continue; // Skip the label
                                    if (textBox_.Text == "Timeout (s)") trigger.TimeoutSecs = int.Parse(textBox_.Text);
                                    else if (textBox_.Text == "Message") trigger.Message = textBox_.Text;
                                }
                            }
                            else if (child is ToggleSwitch toggleSwitch_)
                            {
                                trigger.Enabled = toggleSwitch_.IsOn;
                            }
                        }
                        return trigger;
                    }
                    else
                    {
                        var textBoxes = grid.Children.OfType<TextBox>();
                        return textBoxes
                            // Filter out add new item placeholder
                            .Where(tb => tb.PlaceholderText != "Add new item")
                            .Select(tb => tb.Text).ToList();
                    }
                }
            }

            return string.Empty; // Ideally this should never happen.
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
        public static string NormalToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var words = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new System.Text.StringBuilder();

            foreach (var word in words)
            {
                if (word.Length > 0)
                {
                    result.Append(char.ToUpper(word[0]));
                    if (word.Length > 1)
                        result.Append(word.Substring(1));
                }
            }

            return result.ToString();
        }
        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Settings newSettings = new Settings();
                var properties = typeof(Settings).GetProperties();
                foreach (PropertyInfo prop in properties)
                {
                    object val = null;

                    if (_settingsToIgnore.Contains(prop.Name))
                    {
                        // This setting is not in the UI, so preserve its original value.
                        val = prop.GetValue(_originalSettings);
                    }
                    else
                    {
                        val = GetDetailRow(prop.Name);
                        if (val == null) continue;

                        Type propertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                        if (propertyType == typeof(int) && val.GetType() == typeof(double))
                        {
                            val = Convert.ToInt32(val);
                        }
                        else if (propertyType == typeof(float) && val.GetType() == typeof(double))
                        {
                            val = Convert.ToSingle(val);
                        }
                    }

                    prop.SetValue(newSettings, val);
                }
                JObject settingsObj = JObject.FromObject(newSettings);
                await FileIO.WriteTextAsync(await StorageFile.GetFileFromPathAsync(_settingsFilePath), settingsObj.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                new ContentDialog { 
                    Title = "Settings failed to save",
                    Content = "An error occurred while saving the settings: " + ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot,
                }.ShowAsync();
                return;
            }
            
            new ContentDialog
            {
                Title = "Settings Saved",
                Content = "Settings have been saved successfully.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
            }.ShowAsync();
        }
    }
}
