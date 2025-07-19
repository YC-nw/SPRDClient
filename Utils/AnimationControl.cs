using System.Windows;
using System.Windows.Media.Animation;

namespace SPRDClient.Utils
{
    class AnimationControl
    {
        public static void StartFadeOutAnimation(FrameworkElement element, double time = 0.5)
        {
            DoubleAnimation fadeAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(time),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            fadeAnimation.Completed += (s, _) =>
            {
                element.Visibility = Visibility.Collapsed;
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }
        public static void StartFadeInAnimation(FrameworkElement element, double time = 0.5)
        {
            DoubleAnimation fadeAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(time),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            element.Opacity = 0.0;
            element.Visibility = Visibility.Visible;
            element.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }
    }
}
