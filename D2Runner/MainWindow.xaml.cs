using H.Hooks;
using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.IO;

namespace D2Runner
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private DateTime _startTime;
        private bool _isRunning = false;
        private LowLevelKeyboardHook _keyboardHook;
        private List<TimeSpan> _recordedTimes = new List<TimeSpan>();
        private TaskbarIcon _notifyIcon;
        private MenuItem headerItem;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int VK_R = 0x52;   // Код клавиши 'R'
        private const int VK_F = 0x46;
        private const int VK_TAB = 0x09; // Код клавиши 'Tab'

        private static readonly Dictionary<string, int> keyNameToVk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        { 
            { "TAB", 0x09 },
            { "F1", 0x70 },
            { "F2", 0x71 },
            { "F3", 0x72 },
            { "F4", 0x73 },
            { "F5", 0x74 },
            { "F6", 0x75 },
            { "F7", 0x76 },
            { "F8", 0x77 },
            { "F9", 0x78 },
            { "F10", 0x79 },
            { "F11", 0x7A },
            { "F12", 0x7B },
            { "1", 0x31 },
            { "2", 0x32 },
            { "3", 0x33 },
            { "4", 0x34 },
            { "5", 0x35 },
            { "6", 0x36 },
            { "7", 0x37 },
            { "8", 0x38 },
            { "9", 0x39 },
            { "0", 0x30 },
            { "A", 0x41 },
            { "B", 0x42 },
            { "C", 0x43 },
            { "D", 0x44 },
            { "E", 0x45 },
            { "F", 0x46 },
            { "G", 0x47 },
            { "H", 0x48 },
            { "I", 0x49 },
            { "J", 0x4A },
            { "K", 0x4B },
            { "L", 0x4C },
            { "M", 0x4D },
            { "N", 0x4E },
            { "O", 0x4F },
            { "P", 0x50 },
            { "Q", 0x51 },
            { "R", 0x52 },
            { "S", 0x53 },
            { "T", 0x54 },
            { "U", 0x55 },
            { "V", 0x56 },
            { "W", 0x57 },
            { "X", 0x58 },
            { "Y", 0x59 },
            { "Z", 0x5A },
            { "ENTER", 0x0D },
            { "ESC", 0x1B },
            { "SPACE", 0x20 },
            { "BACKSPACE", 0x08 },
            { "LCTRL", 0xA2 },
            { "RCTRL", 0xA3 },
            { "LSHIFT", 0xA0 },
            { "RSHIFT", 0xA1 },
            { "LALT", 0xA4 },
            { "RALT", 0xA5 }
        };

        private async Task ExecuteMacroFromText(string macroText)
        {
            if (string.IsNullOrWhiteSpace(macroText))
                return;

            var keys = macroText.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var keyName in keys)
            {
                if (keyNameToVk.TryGetValue(keyName, out int vkCode))
                {
                    PressKey(vkCode);
                    await Task.Delay(500);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Неизвестная клавиша: {keyName}");
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            SetupNotifyIcon();

            // Таймер с интервалом 100 мс — 10 обновлений в секунду
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100); // ← ключевое изменение!
            _timer.Tick += Timer_Tick;

            Loaded += MainWindow_Loaded;
            SetupGlobalHook();
        }

        private void SetupNotifyIcon()
        {
            
            var contextMenu = new ContextMenu();

            var timerMenu = new MenuItem
            {
                Header = "Таймер"
            };

            var clearItem = new MenuItem
            {
                Header = "Очистить список"
            };
            clearItem.Click += (s, e) => ResetStat();

            var deleteLast = new MenuItem
            {
                Header = "Удалить последнюю запись"
            };
            deleteLast.Click += (s, e) => DeleteLast();

            timerMenu.Items.Add(clearItem);
            timerMenu.Items.Add(deleteLast);

            var statsMenu = new MenuItem
            {
                Header = "Статистика"
            };

            headerItem = new MenuItem
            {
                Header = "new",
                IsEnabled = false, // ← нельзя кликнуть
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Gray,
            };

            // Пункт "Загрузить статистику"
            var loadItem = new MenuItem
            {
                Header = "Загрузить",
                Icon = null,
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };
            loadItem.Click += (s, e) => LoadStatistics();

            // Пункт "Сохранить статистику"
            var saveItem = new MenuItem
            {
                Header = "Сохранить",
                Icon = null,
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };
            saveItem.Click += (s, e) => SaveStatistics();

            var showList = new MenuItem
            {
                Header = "Показать список",
                Icon = null,
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };
            showList.Click += (s, e) =>
            {
                this.Visibility = Visibility.Visible;
                this.Activate();
                this.Focus();

                var statsWindow = new StatisticsWindow(_recordedTimes, UpdateStatistics);
                statsWindow.Owner = this;
                statsWindow.ShowDialog();
            };

            statsMenu.Items.Add(headerItem);
            statsMenu.Items.Add(new Separator());
            statsMenu.Items.Add(loadItem);
            statsMenu.Items.Add(saveItem);
            statsMenu.Items.Add(showList);

            var macroItem = new MenuItem
            {
                Header = "Макрос",
            };
            macroItem.Click += (s, e) =>
            {
                this.Visibility = Visibility.Visible;
                this.Activate();
                this.Focus();

                var macroWindow = new MacroWindow();
                macroWindow.Owner = this;
                macroWindow.ShowDialog();
            };

            var blizzlessItem = new MenuItem
            {
                Header = "Blizzless",
                Icon = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/blizzless.ico")),
                    Width = 16,
                    Height = 16
                },
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };
            blizzlessItem.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://blizzless.info/",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось открыть сайт: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            var exitItem = new MenuItem
            {
                Header = "Выход",
            };
            exitItem.Click += (s, e) => Application.Current.Shutdown();

            
            contextMenu.Items.Add(timerMenu);
            contextMenu.Items.Add(statsMenu);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(macroItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(blizzlessItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon = new TaskbarIcon
            {
                IconSource = new BitmapImage(new Uri("pack://application:,,,/logo.ico")),
                ToolTipText = "D2Runner Timer",
                Visibility = Visibility.Visible,
                ContextMenu = contextMenu
            };
        }

        private void SaveStatistics()
        {
            // Показываем и активируем окно — иначе диалог закроется мгновенно
            this.Visibility = Visibility.Visible;
            this.Activate();
            this.Focus();

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить статистику",
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = "D2Runner_Statistics.txt",
                DefaultExt = ".txt"
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                try
                {
                    var lines = _recordedTimes.Select(time => time.ToString("c"));
                    File.WriteAllLines(dialog.FileName, lines);

                    headerItem.Header = System.IO.Path.GetFileName(dialog.FileName);

                    MessageBox.Show($"Сохранено {_recordedTimes.Count} записей.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadStatistics()
        {
            // Показываем и активируем окно — иначе диалог закроется мгновенно
            this.Visibility = Visibility.Visible;
            this.Activate();
            this.Focus();

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Загрузить статистику",
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dialog.FileName);
                    var loadedTimes = new List<TimeSpan>();

                    foreach (var line in lines)
                    {
                        if (TimeSpan.TryParse(line, out TimeSpan time))
                        {
                            loadedTimes.Add(time);
                        }
                        else
                        {
                            // Пропускаем невалидные строки
                            System.Diagnostics.Debug.WriteLine($"Пропущена невалидная строка: {line}");
                        }
                    }

                    _recordedTimes = loadedTimes;
                    UpdateStatistics();
                    headerItem.Header = System.IO.Path.GetFileName(dialog.FileName);

                    MessageBox.Show($"Загружено {loadedTimes.Count} записей.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var screen = SystemParameters.WorkArea;
            Left = (screen.Width - Width) / 2;
            Top = screen.Top + 10;
            Topmost = true;
            Focus();
        }
        
        private void SetupGlobalHook()
        {
            _keyboardHook = new LowLevelKeyboardHook();
            _keyboardHook.Down += async (s, args) =>
            {
                if (args.CurrentKey.Equals(H.Hooks.Key.Escape))
                {
                    StopTimer();
                }
                if (args.Keys.Are(H.Hooks.Key.Control, H.Hooks.Key.Space) || args.Keys.Are(H.Hooks.Key.Alt, H.Hooks.Key.Q))
                {
                    StartTimer();
                    string macroText = Properties.Settings.Default.MacroText;
                    await ExecuteMacroFromText(macroText);
                    //await ExecuteMacro();
                }
                if (args.CurrentKey.Equals(H.Hooks.Key.LShift))
                {
                    
                }
            };
            _keyboardHook.Start();
        }

        private void PressKey(int vkCode)
        {
            // Нажать клавишу
            keybd_event((byte)vkCode, 0, 0, UIntPtr.Zero);
            // Отпустить клавишу
            keybd_event((byte)vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        //private async Task ExecuteMacro()
        //{
        //    // Макрос: R → пауза → R → пауза → TAB
        //    PressKey(VK_R);
        //    await Task.Delay(500);

        //    PressKey(VK_F);
        //    await Task.Delay(500);

        //    PressKey(VK_TAB);
        //}

        private void ResetStat()
        {
            _recordedTimes.Clear();
            headerItem.Header = "new";
            UpdateStatistics();
        }

        public void DeleteLast()
        {
            if (_recordedTimes.Count > 0)
            {
                _recordedTimes.RemoveAt(_recordedTimes.Count - 1);
                UpdateStatistics();
            }
        }

        private void StartTimer()
        {
            _startTime = DateTime.Now;
            _isRunning = true;
            _timer.Start();
        }

        private void StopTimer()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _timer.Stop();
            var elapsed = DateTime.Now - _startTime;
            _recordedTimes.Add(elapsed);
            SetTimerText(elapsed);
            UpdateStatistics();
        }
        
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_isRunning) return;
            var elapsed = DateTime.Now - _startTime;
            SetTimerText(elapsed);
        }

        private void SetTimerText(TimeSpan time)
        {
            int minutes = (int)time.TotalMinutes;
            int seconds = time.Seconds;
            int msecs = time.Milliseconds;

            // Форматируем каждую цифру
            Dispatcher.Invoke(() =>
            {
                MinTens.Content = (minutes / 10).ToString();
                MinOnes.Content = (minutes % 10).ToString();
                SecTens.Content = (seconds / 10).ToString();
                SecOnes.Content = (seconds % 10).ToString();
                MSecTens.Content = (msecs / 100).ToString();
                MSecOnes.Content = ((msecs / 10) % 10).ToString();
            });
                
        }

        private void UpdateStatistics()
        {
            if (_recordedTimes.Count == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    MinLabel.Content = "";
                    MaxLabel.Content = "";
                    AvgLabel.Content = "";
                });
                return;
            }

            var min = _recordedTimes.Min();
            var max = _recordedTimes.Max();
            var avg = TimeSpan.FromTicks((long)_recordedTimes.Average(t => t.Ticks));
            var cnt = _recordedTimes.Count;

            Dispatcher.Invoke(() =>
            {
                MinLabel.Content = $"{FormatTime(min)}";
                MaxLabel.Content = $"{FormatTime(max)}";
                AvgLabel.Content = $"{FormatTime(avg)} ‣ {cnt}";
            });
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}.{(time.Milliseconds / 10):D2}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _keyboardHook?.Dispose();
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}