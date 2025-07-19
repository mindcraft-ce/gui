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
using mindcraft_ce.Models;
using mindcraft_ce.ViewModels;
using Newtonsoft.Json.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace mindcraft_ce.Views
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
            detailPanel.Opacity = 0; // insta hide for "fade out"

            detailPanel.Children.Clear();

            var elementsToAnimate = new List<UIElement>();

            TextBlock selectedAgentTextBlock = new TextBlock
            {
                Text = "Selected Agent",
                FontSize = 24,
                Margin = new Thickness(0, 0, 0, 20)
            };


            detailPanel.Children.Add(selectedAgentTextBlock);

            var properties = typeof(Agent).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var propName = prop.Name;
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

            if (type == typeof(string))
            {
                valueElement = new TextBox
                {
                    Text = valueObj?.ToString() ?? "",
                    PlaceholderText = "Not Set",
                    FontSize = fontSize,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                    IsReadOnly = false
                };
            }
            else if (type == typeof(int))
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
            else if (type == typeof(Model))
            {
                var model = valueObj as Model;

                var grid = new Grid
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4),
                };

                // 3 rows, 2 columns
                for (int i = 0; i < 3; i++)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                void AddLabeledField(string label, string value, int row)
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

                    var textBox = new TextBox
                    {
                        Text = value ?? "",
                        PlaceholderText = "Not Set",
                        FontSize = fontSize,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                    };
                    Grid.SetRow(textBox, row);
                    Grid.SetColumn(textBox, 1);
                    grid.Children.Add(textBox);
                }

                AddLabeledField("Name", model?.Name, 0);
                AddLabeledField("API", model?.Api, 1);
                AddLabeledField("URL", model?.Url, 2);

                valueElement = grid;
            }
            else if (type == typeof(Modes))
            {
                var modes = valueObj as Modes;

                Grid grid = new Grid
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4),
                };

                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                void AddLabeledField(string label, bool value, int row)
                {
                    var checkBox = new ToggleSwitch
                    {
                        IsOn = value,
                        FontSize = fontSize,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
                        // MaxWidth = 50,
                        Margin = new Thickness(10, 0, 10, 0),
                    };
                    Grid.SetRow(checkBox, row);
                    Grid.SetColumn(checkBox, 0);
                    grid.Children.Add(checkBox);

                    var labelText = new TextBlock
                    {
                        Text = label,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = fontSize,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0),
                    };
                    Grid.SetRow(labelText, row);
                    Grid.SetColumn(labelText, 1);
                    grid.Children.Add(labelText);
                }

                int i = 0;
                foreach (var property in typeof(Modes).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                    bool val = property.GetValue(modes) is bool b && b;
                    string label = System.Text.RegularExpressions.Regex
                        .Replace(property.Name, "(?<!^)([A-Z])", " $1"); // PascalCase -> Title Case

                    AddLabeledField(label, val, i);
                    i++;
                }

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
    }
}
