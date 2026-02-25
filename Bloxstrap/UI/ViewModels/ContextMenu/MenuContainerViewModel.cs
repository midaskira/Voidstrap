using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Voidstrap.UI.Chat
{
    public class MenuContainerViewModel : ObservableObject
    {
        private double _brightness = App.Settings.Prop.Brightness;

        public double Brightness
        {
            get => _brightness;
            set
            {
                double clamped = Math.Clamp(value, 0, 100);
                if (SetProperty(ref _brightness, clamped))
                {
                    App.Settings.Prop.Brightness = clamped;
                    App.Settings.Save();
                    OnPropertyChanged(nameof(BrightnessDisplay));
                }
            }
        }

        public string BrightnessDisplay =>
            Brightness == 50
                ? "(Disabled)"
                : $"{Brightness:0}%";
    }
}