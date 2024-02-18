using Automation.GenerativeAI.UX.Services;
using System.Windows;

namespace GenAIApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            ServiceContainer.Register(new ChatService());
            InitializeComponent();
        }

        private void ChatView_ButtonClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Button Clicked!!");
        }
    }
}