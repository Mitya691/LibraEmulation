using LibraEmulation;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Windows.Input;
using ScaleEmulator;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Windows.Threading;

namespace LibraEmulation
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SerialPortService _serialPortService;
        private bool _isConnected = false;
        private string _logText = "";
        private string _startStopButtonText = "Старт";

        public ObservableCollection<string> AvailablePorts { get; set; }
        public ObservableCollection<int> BaudRates { get; set; }
        public ObservableCollection<string> Parities { get; set; }

        private string _selectedPort;
        public string SelectedPort
        {
            get => _selectedPort;
            set { _selectedPort = value; OnPropertyChanged(nameof(SelectedPort)); }
        }

        private int _selectedBaud;
        public int SelectedBaud
        {
            get => _selectedBaud;
            set { _selectedBaud = value; OnPropertyChanged(nameof(SelectedBaud)); }
        }

        private string _selectedParity;
        public string SelectedParity
        {
            get => _selectedParity;
            set { _selectedParity = value; OnPropertyChanged(nameof(SelectedParity)); }
        }

        private double _weight;
        public double Weight
        {
            get => _weight;
            set { _weight = value; OnPropertyChanged(nameof(Weight)); }
        }

        public byte FixedAddressOne
        {
            get => _serialPortService.LibraOne.FixedAddress;
            set { _serialPortService.LibraOne.FixedAddress = value; OnPropertyChanged(nameof(FixedAddressOne)); }
        }

        public byte FixedAddressTwo
        {
            get => _serialPortService.LibraTwo.FixedAddress;
            set { _serialPortService.LibraTwo.FixedAddress = value; OnPropertyChanged(nameof(FixedAddressTwo)); }
        }

        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(nameof(LogText)); }
        }

        // ---------------------------
        // Свойства для эмуляции циклического взвешивания зерна
        // ---------------------------
        // Текущий вес, который вводится пользователем (в кг)
        public double CurrentWeightOne
        {
            get => _serialPortService.LibraOne.CurrentWeight;
            set
            {
                if (_serialPortService.LibraOne.CurrentWeight != value)
                {
                    _serialPortService.LibraOne.CurrentWeight = value;
                    OnPropertyChanged(nameof(CurrentWeightOne));
                }
            }
        }

        public double CurrentWeightTwo
        {
            get => _serialPortService.LibraTwo.CurrentWeight;
            set
            {
                if (_serialPortService.LibraTwo.CurrentWeight != value)
                {
                    _serialPortService.LibraTwo.CurrentWeight = value;
                    OnPropertyChanged(nameof(CurrentWeightTwo));
                }
            }
        }

        // Накопленный вес за всё время работы (в кг)
        public double CumulativeWeightOne
        {
            get => _serialPortService.LibraOne.CumulativeWeight;
            set
            {
                if (_serialPortService.LibraOne.CumulativeWeight != value)
                {
                    _serialPortService.LibraOne.CumulativeWeight = value;
                    OnPropertyChanged(nameof(CumulativeWeightOne));
                }
            }
        }

        public double CumulativeWeightTwo
        {
            get => _serialPortService.LibraTwo.CumulativeWeight;
            set
            {
                if (_serialPortService.LibraTwo.CumulativeWeight != value)
                {
                    _serialPortService.LibraTwo.CumulativeWeight = value;
                    OnPropertyChanged(nameof(CumulativeWeightTwo));
                }
            }
        }

        // Производительность (в тоннах/час)
        public double PerformanceOne
        {
            get => _serialPortService.LibraOne.Performance;
            set
            {
                if (_serialPortService.LibraOne.Performance != value)
                {
                    _serialPortService.LibraOne.Performance = value;
                    OnPropertyChanged(nameof(PerformanceOne));
                }
            }
        }

        public double PerformanceTwo
        {
            get => _serialPortService.LibraTwo.Performance;
            set
            {
                if (_serialPortService.LibraTwo.Performance != value)
                {
                    _serialPortService.LibraTwo.Performance = value;
                    OnPropertyChanged(nameof(PerformanceTwo));
                }
            }
        }

        // Время старта эмуляции (для расчёта производительности)
        private DateTime _startTime;

        // Команда сброса счетчика
        public ICommand ResetCounterCommand { get; set; }

        // Команда запуска/остановки эмуляции (COM-порт, весы и т.д.)
        public ICommand StartStopCommand { get; set; }

        // Команда для эмуляции цикла (можно запускать автоматически, см. ниже)
        // Например, можно использовать DispatcherTimer, поэтому отдельная команда не нужна.

        // Таймер для эмуляции цикла (10 секунд)
        private readonly DispatcherTimer _cycleTimer;

        public string StartStopButtonText
        {
            get => _startStopButtonText;
            set { _startStopButtonText = value; OnPropertyChanged(nameof(StartStopButtonText)); }
        }

        public bool IsUspokoenieOne
        {
            get => _serialPortService.LibraOne.IsUspokoenie;
            set
            {
                if (_serialPortService.LibraOne.IsUspokoenie != value)
                {
                    _serialPortService.LibraOne.IsUspokoenie = value;
                    OnPropertyChanged(nameof(IsUspokoenieOne));
                }
            }
        }

        public bool IsUspokoenieTwo
        {
            get => _serialPortService.LibraTwo.IsUspokoenie;
            set
            {
                if (_serialPortService.LibraTwo.IsUspokoenie != value)
                {
                    _serialPortService.LibraTwo.IsUspokoenie = value;
                    OnPropertyChanged(nameof(IsUspokoenieTwo));
                }
            }
        }

        public bool IsPeregOne
        {
            get => _serialPortService.LibraOne.IsPereg;
            set
            {
                if (_serialPortService.LibraOne.IsPereg != value)
                {
                    _serialPortService.LibraOne.IsPereg = value;
                    OnPropertyChanged(nameof(IsPeregOne));
                }
            }
        }

        public bool IsPeregTwo
        {
            get => _serialPortService.LibraTwo.IsPereg;
            set
            {
                if (_serialPortService.LibraTwo.IsPereg != value)
                {
                    _serialPortService.LibraTwo.IsPereg = value;
                    OnPropertyChanged(nameof(IsPeregTwo));
                }
            }
        }

        public bool IsReweighingOne
        {
            get => _serialPortService.LibraOne.IsReweighing;
            set
            {
                if (_serialPortService.LibraOne.IsReweighing != value)
                {
                    _serialPortService.LibraOne.IsReweighing = value;
                    OnPropertyChanged(nameof(IsReweighingOne));
                }
            }
        }

        public bool IsReweighingTwo
        {
            get => _serialPortService.LibraTwo.IsReweighing;
            set
            {
                if (_serialPortService.LibraTwo.IsReweighing != value)
                {
                    _serialPortService.LibraTwo.IsReweighing = value;
                    OnPropertyChanged(nameof(IsReweighingTwo));
                }
            }
        }

        public bool HasErrorOne
        {
            get => _serialPortService.LibraOne.HasError;
            set
            {
                if (_serialPortService.LibraOne.HasError != value)
                {
                    _serialPortService.LibraOne.HasError = value;
                    OnPropertyChanged(nameof(HasErrorOne));
                }
            }
        }

        public bool HasErrorTwo
        {
            get => _serialPortService.LibraTwo.HasError;
            set
            {
                if (_serialPortService.LibraTwo.HasError != value)
                {
                    _serialPortService.LibraTwo.HasError = value;
                    OnPropertyChanged(nameof(HasErrorTwo));
                }
            }
        }

        public bool IsStopModeOne
        {
            get => _serialPortService.LibraOne.IsStopMode;
            set
            {
                if (_serialPortService.LibraOne.IsStopMode != value)
                {
                    _serialPortService.LibraOne.IsStopMode = value;
                    OnPropertyChanged(nameof(IsStopModeOne));
                }
            }
        }

        public bool IsStopModeTwo
        {
            get => _serialPortService.LibraTwo.IsStopMode;
            set
            {
                if (_serialPortService.LibraTwo.IsStopMode != value)
                {
                    _serialPortService.LibraTwo.IsStopMode = value;
                    OnPropertyChanged(nameof(IsStopModeTwo));
                }
            }
        }

        public bool IsCycleCompleteOne
        {
            get => _serialPortService.LibraOne.IsCycleComplete;
            set
            {
                if (_serialPortService.LibraOne.IsCycleComplete != value)
                {
                    _serialPortService.LibraOne.IsCycleComplete = value;
                    OnPropertyChanged(nameof(IsCycleCompleteOne));
                }
            }
        }

        public bool IsCycleCompleteTwo
        {
            get => _serialPortService.LibraTwo.IsCycleComplete;
            set
            {
                if (_serialPortService.LibraTwo.IsCycleComplete != value)
                {
                    _serialPortService.LibraTwo.IsCycleComplete = value;
                    OnPropertyChanged(nameof(IsCycleCompleteOne));
                }
            }
        }

        public bool IsPausedOne
        {
            get => _serialPortService.LibraOne.IsPaused;
            set
            {
                if (_serialPortService.LibraOne.IsPaused != value)
                {
                    _serialPortService.LibraOne.IsPaused = value;
                    OnPropertyChanged(nameof(IsPausedOne));
                }
            }
        }

        public bool IsPausedTwo
        {
            get => _serialPortService.LibraTwo.IsPaused;
            set
            {
                if (_serialPortService.LibraTwo.IsPaused != value)
                {
                    _serialPortService.LibraTwo.IsPaused = value;
                    OnPropertyChanged(nameof(IsPausedTwo));
                }
            }
        }

        public bool IsLoadingOne
        {
            get => _serialPortService.LibraOne.IsLoading;
            set
            {
                if (_serialPortService.LibraOne.IsLoading != value)
                {
                    _serialPortService.LibraOne.IsLoading = value;
                    OnPropertyChanged(nameof(IsLoadingOne));
                }
            }
        }

        public bool IsLoadingTwo
        {
            get => _serialPortService.LibraTwo.IsLoading;
            set
            {
                if (_serialPortService.LibraTwo.IsLoading != value)
                {
                    _serialPortService.LibraTwo.IsLoading = value;
                    OnPropertyChanged(nameof(IsLoadingTwo));
                }
            }
        }

        public bool IsUnloadingOne
        {
            get => _serialPortService.LibraOne.IsUnloading;
            set
            {
                if (_serialPortService.LibraOne.IsUnloading != value)
                {
                    _serialPortService.LibraOne.IsUnloading = value;
                    OnPropertyChanged(nameof(IsUnloadingOne));
                }
            }
        }

        public bool IsUnloadingTwo
        {
            get => _serialPortService.LibraTwo.IsUnloading;
            set
            {
                if (_serialPortService.LibraTwo.IsUnloading != value)
                {
                    _serialPortService.LibraTwo.IsUnloading = value;
                    OnPropertyChanged(nameof(IsUnloadingTwo));
                }
            }
        }

        public bool IsOnPassOne
        {
            get => _serialPortService.LibraOne.IsOnPass;
            set
            {
                if (_serialPortService.LibraOne.IsOnPass != value)
                {
                    _serialPortService.LibraOne.IsOnPass = value;
                    OnPropertyChanged(nameof(IsOnPassOne));
                }
            }
        }

        public bool IsOnPassTwo
        {
            get => _serialPortService.LibraTwo.IsOnPass;
            set
            {
                if (_serialPortService.LibraTwo.IsOnPass != value)
                {
                    _serialPortService.LibraTwo.IsOnPass = value;
                    OnPropertyChanged(nameof(IsOnPassTwo));
                }
            }
        }

        public MainViewModel()
        {
            AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames());
            BaudRates = new ObservableCollection<int>(new int[] { 9600, 19200, 38400, 57600, 115200 });
            Parities = new ObservableCollection<string>(Enum.GetNames(typeof(Parity)));

            SelectedPort = AvailablePorts.Any() ? AvailablePorts.First() : "COM1";
            SelectedBaud = 9600;
            SelectedParity = Parity.None.ToString();
            Weight = 0.5;

            StartStopCommand = new RelayCommand(ExecuteStartStop);

            _serialPortService = new SerialPortService();
            _serialPortService.LogMessage += AppendLog;
            _serialPortService.ErrorMessage += AppendLog;
            _serialPortService.HandshakeCompleted += () =>
            {
                AppendLog("Соединение установлено.");
                // Здесь можно вызывать диалог через сервис, если требуется
            };

            // Инициализируем свойства для эмуляции цикла
            CurrentWeightOne = 0;
            CurrentWeightTwo = 0;
            CumulativeWeightOne = 0;
            CumulativeWeightTwo = 0;
            PerformanceOne = 0;
            PerformanceTwo = 0;
            _startTime = DateTime.Now;

            ResetCounterCommand = new RelayCommand(o => ResetCounter());
            // Команда для запуска/остановки (здесь можно добавить логику работы с COM-портом)

            _cycleTimer = new DispatcherTimer();
            _cycleTimer.Interval = TimeSpan.FromSeconds(10);
            _cycleTimer.Tick += CycleTimer_Tick;
        }

        // Метод, вызываемый каждые 10 секунд (эмуляция цикла)
        private void CycleTimer_Tick(object sender, EventArgs e)
        {
            // В цикле весы "накапливают" зерно.
            // Здесь мы прибавляем текущий вес (введённый пользователем) к накопленному значению.
            CumulativeWeightOne += CurrentWeightOne;
            CumulativeWeightTwo += CurrentWeightTwo;
            // Можно также сбрасывать CurrentWeight после цикла, если симулируется разрядка зерна:
            // CurrentWeight = 0;

            // Обновляем производительность
            UpdatePerformance();

            // Логируем событие цикла
            LogText += $"{DateTime.Now:HH:mm:ss} Цикл завершён. Накоплено(весы 1): {CumulativeWeightOne} кг\n";
            LogText += $"{DateTime.Now:HH:mm:ss} Цикл завершён. Накоплено(весы 2): {CumulativeWeightTwo} кг\n";
        }

        // Расчёт производительности (тонн/час)
        private void UpdatePerformance()
        {
            double elapsedHours = (DateTime.Now - _startTime).TotalHours;
            if (elapsedHours > 0)
            {
                // Преобразуем накопленный вес в тонны (1 тонна = 1000 кг)
                PerformanceOne = (CumulativeWeightOne / 1000) / elapsedHours;
                PerformanceTwo = (CumulativeWeightTwo / 1000) / elapsedHours;
            }
        }

        // Команда сброса накопленного счетчика
        private void ResetCounter()
        {
            CumulativeWeightOne = 0;
            CurrentWeightTwo = 0;
            _startTime = DateTime.Now;
            PerformanceOne = 0;
            PerformanceTwo = 0;
            LogText += $"{DateTime.Now:HH:mm:ss} Счетчик сброшен.\n";
        }

        private void ExecuteStartStop(object parameter)
        {
            if (!_isConnected)
            {
                try
                {
                    // Запускаем подключение к COM-порту через сервис
                    _serialPortService.Start(SelectedPort, SelectedBaud,
                        (Parity)Enum.Parse(typeof(Parity), SelectedParity));
                    _isConnected = true;

                    // Обновляем UI – меняем текст кнопки и логгируем
                    StartStopButtonText = "Стоп";
                    AppendLog($"{DateTime.Now:HH:mm:ss} Порт открыт. Эмуляция запущена.");

                    // Запускаем эмуляцию рабочего цикла (например, каждые 10 секунд)
                    _startTime = DateTime.Now;
                    _cycleTimer.Interval = TimeSpan.FromSeconds(10);
                    _cycleTimer.Start();
                }
                catch (Exception ex)
                {
                    AppendLog("Ошибка: " + ex.Message);
                }
            }
            else
            {
                // Останавливаем работу COM-порта и эмуляцию
                _serialPortService.Stop();
                _isConnected = false;
                StartStopButtonText = "Старт";
                AppendLog($"{DateTime.Now:HH:mm:ss} Порт закрыт. Эмуляция остановлена.");
                _cycleTimer.Stop();
            }
        }

        private void AppendLog(string message)
        {
            LogText += $"{DateTime.Now:HH:mm:ss.fff} {message}\n";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
