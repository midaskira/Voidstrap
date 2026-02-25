using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace Voidstrap.UI.Elements.Settings
{
    public class SearchablePage : Page
    {
        public virtual void PerformSearch(string query)
        {
            HighlightMatches(this, query?.ToLower() ?? "");
        }

        private void HighlightMatches(DependencyObject parent, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBlock textBlock)
                {
                    if (!string.IsNullOrWhiteSpace(textBlock.Text) &&
                        textBlock.Text.ToLower().Contains(query))
                    {
                        textBlock.Background = System.Windows.Media.Brushes.Yellow;
                    }
                    else
                    {
                        textBlock.Background = System.Windows.Media.Brushes.Transparent;
                    }
                }

                HighlightMatches(child, query);
            }
        }
    }
}