using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BrightnessControl
{
    public partial class MainWindow : Window
    {
        private List<MonitorInfo> _monitors = new();
        private readonly Dictionary<MonitorInfo, TextBlock> _valueLabels = new();
        private readonly Dictionary<MonitorInfo, Slider> _sliders = new();
        private bool _suppressSyncEvent;

        public MainWindow()
        {
            InitializeComponent();
            Closing += (s, e) => MonitorBrightnessController.ReleaseAllHandles();
            LoadMonitors();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            MonitorBrightnessController.ReleaseAllHandles();
            LoadMonitors();
        }

        private void LoadMonitors()
        {
            MonitorPanel.Children.Clear();
            _valueLabels.Clear();
            _sliders.Clear();

            _monitors = MonitorBrightnessController.GetAllMonitors();

            if (_monitors.Count == 0)
            {
                MonitorPanel.Children.Add(new TextBlock
                {
                    Text = "No controllable monitors found.",
                    Style = (Style)FindResource("SubText")
                });
                SyncValueText.Text = "—";
                return;
            }

            foreach (var monitor in _monitors)
            {
                var card = BuildMonitorCard(monitor);
                MonitorPanel.Children.Add(card);
            }

            // Initialize sync slider to the average current brightness, without triggering a mass-set
            _suppressSyncEvent = true;
            uint avg = 0;
            foreach (var m in _monitors) avg += m.CurrentBrightness;
            avg /= (uint)_monitors.Count;
            SyncSlider.Value = avg;
            SyncValueText.Text = $"{avg}%";
            _suppressSyncEvent = false;
        }

        private Border BuildMonitorCard(MonitorInfo monitor)
        {
            var card = new Border { Style = (Style)FindResource("CardStyle") };
            var stack = new StackPanel();

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = monitor.Name,
                Style = (Style)FindResource("TitleText")
            };
            Grid.SetColumn(titleText, 0);

            var valueText = new TextBlock
            {
                Text = $"{monitor.CurrentBrightness}%",
                Style = (Style)FindResource("ValueText")
            };
            Grid.SetColumn(valueText, 1);
            _valueLabels[monitor] = valueText;

            headerGrid.Children.Add(titleText);
            headerGrid.Children.Add(valueText);

            var subText = new TextBlock
            {
                Text = monitor.IsInternal ? "Internal display" : "External · DDC/CI",
                Style = (Style)FindResource("SubText"),
                Margin = new Thickness(0, 2, 0, 8)
            };

            var slider = new Slider
            {
                Style = (Style)FindResource("AccentSlider"),
                Minimum = monitor.MinBrightness,
                Maximum = monitor.MaxBrightness,
                Value = monitor.CurrentBrightness,
                TickFrequency = 1,
                IsSnapToTickEnabled = true
            };
            slider.ValueChanged += (s, e) =>
            {
                var val = (uint)e.NewValue;
                monitor.CurrentBrightness = val;
                MonitorBrightnessController.SetBrightness(monitor, val);
                valueText.Text = $"{val}%";
            };
            _sliders[monitor] = slider;

            var footerGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var defaultText = new TextBlock
            {
                Text = $"Default: {monitor.DefaultBrightness}%",
                Style = (Style)FindResource("SubText"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(defaultText, 0);

            var resetButton = new Button
            {
                Content = "Reset",
                Style = (Style)FindResource("GhostButton")
            };
            Grid.SetColumn(resetButton, 1);
            resetButton.Click += (s, e) =>
            {
                slider.Value = monitor.DefaultBrightness;
            };

            footerGrid.Children.Add(defaultText);
            footerGrid.Children.Add(resetButton);

            stack.Children.Add(headerGrid);
            stack.Children.Add(subText);
            stack.Children.Add(slider);
            stack.Children.Add(footerGrid);

            card.Child = stack;
            return card;
        }

        private void SyncSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSyncEvent) return;

            var val = (uint)e.NewValue;
            SyncValueText.Text = $"{val}%";

            foreach (var monitor in _monitors)
            {
                // Clamp to each monitor's own range (internal is 0-100, external may vary)
                var clamped = val > monitor.MaxBrightness ? monitor.MaxBrightness
                            : val < monitor.MinBrightness ? monitor.MinBrightness
                            : val;

                monitor.CurrentBrightness = clamped;
                MonitorBrightnessController.SetBrightness(monitor, clamped);

                if (_sliders.TryGetValue(monitor, out var slider))
                    slider.Value = clamped; // triggers its own ValueChanged, updates its own label too

                if (_valueLabels.TryGetValue(monitor, out var label))
                    label.Text = $"{clamped}%";
            }
        }
    }
}