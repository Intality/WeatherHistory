using System;
using System.Windows;

namespace WeatherHistory
{
    public partial class InputDialog : Window
    {
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        public InputDialog(double currentLat, double currentLon)
        {
            InitializeComponent();

            LatInput.Text = currentLat.ToString();
            LonInput.Text = currentLon.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(LatInput.Text, out double lat) ||
                !double.TryParse(LonInput.Text, out double lon))
            {
                MessageBox.Show("Enter valid numeric latitude and longitude.",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Latitude = lat;
            Longitude = lon;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
