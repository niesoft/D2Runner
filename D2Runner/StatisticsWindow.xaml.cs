using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace D2Runner
{
    public partial class StatisticsWindow : Window
    {
        private List<TimeSpan> _sourceList;
        private Action _onListChanged;

        public StatisticsWindow(List<TimeSpan> recordedTimes, Action onListChanged)
        {
            InitializeComponent();

            _sourceList = recordedTimes;
            _onListChanged = onListChanged;

            LoadData();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ItemsList.SelectedItem != null)
            {
                var selectedItem = ItemsList.SelectedItem;
                var index = ItemsList.Items.IndexOf(selectedItem);

                if (index >= 0 && index < _sourceList.Count)
                {
                    _sourceList.RemoveAt(index);
                    LoadData(); // Перезагружаем список
                    _onListChanged?.Invoke(); // Обновляем статистику в MainWindow
                }
            }
        }

        private void LoadData()
        {
            var items = new List<dynamic>();
            for (int i = 0; i < _sourceList.Count; i++)
            {
                items.Add(new
                {
                    Index = i + 1,
                    DisplayTime = FormatTime(_sourceList[i]),
                    OriginalIndex = i
                });
            }

            ItemsList.ItemsSource = items;
        }


        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}.{(time.Milliseconds / 10):D2}";
        }
    }
}