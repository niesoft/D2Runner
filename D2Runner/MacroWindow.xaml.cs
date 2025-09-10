using System.Windows;

namespace D2Runner
{
    public partial class MacroWindow : Window
    {
        public MacroWindow()
        {
            InitializeComponent();

            // Загружаем сохранённый текст при открытии окна
            MacroText.Text = Properties.Settings.Default.MacroText;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем текст в настройки
            Properties.Settings.Default.MacroText = MacroText.Text;
            Properties.Settings.Default.Save(); // ← Сохраняем в файл

            MessageBox.Show("Макрос сохранён!", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Через пробел перечисли клавиши которые необходимо нажать при запуске таймера.\nПример: TAB F1 1 F", "Справка", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}