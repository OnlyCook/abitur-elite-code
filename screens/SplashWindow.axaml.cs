using Avalonia.Controls;
using System.Threading.Tasks;

namespace AbiturEliteCode
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public async Task AnimateProgressAsync()
        {
            for (int i = 0; i <= 100; i += 2)
            {
                LoadingBar.Value = i;
                await Task.Delay(15);
            }
        }
    }
}