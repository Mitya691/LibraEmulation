using LibraEmulation;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Windows.Input;
using ScaleEmulator;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        private byte _fixedAddress;
        public byte FixedAddress
        {
            get => _fixedAddress;
            set { _fixedAddress = value; OnPropertyChanged(nameof(FixedAddress)); }
        }

        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(nameof(LogText)); }
        }

        public string StartStopButtonText
        {
            get => _startStopButtonText;
            set { _startStopButtonText = value; OnPropertyChanged(nameof(StartStopButtonText)); }
        }

        private bool _isUspokoenie;
        public bool IsUspokoenie
        {
            get => _isUspokoenie;
            set { _isUspokoenie = value; OnPropertyChanged(nameof(IsUspokoenie)); }
        }

        private bool _isPereg;
        public bool IsPereg
        {
            get => _isPereg;
            set { _isPereg = value; OnPropertyChanged(nameof(IsPereg)); }
        }

        private bool _isReweighing;
        public bool IsReweighing
        {
            get => _isReweighing;
            set
            {
                if (_isReweighing != value)
                {
                    _isReweighing = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.IsReweighing = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(IsReweighing));
                }
            }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set
            {
                if (_hasError != value)
                {
                    _hasError = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.HasError = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        private bool _isStopMode;
        public bool IsStopMode
        {
            get => _isStopMode;
            set
            {
                if (_isStopMode != value)
                {
                    _isStopMode = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.IsStopMode = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(IsStopMode));
                }
            }
        }

        private bool _isCycleComplete;
        public bool IsCycleComplete
        {
            get => _isCycleComplete;
            set
            {
                if (_isCycleComplete != value)
                {
                    _isCycleComplete = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.IsCycleComplete = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(IsCycleComplete));
                }
            }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.IsPaused = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(IsPaused));
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.IsLoading = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        private bool _isUnloading;
        public bool IsUnloading
        {
            get => _isUnloading;
            set
            {
                if (_isUnloading != value)
                {
                    _isUnloading = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.IsUnloading = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(IsUnloading));
                }
            }
        }

        private bool _isOnPass;
        public bool IsOnPass
        {
            get => _isOnPass;
            set
            {
                if (_isOnPass != value)
                {
                    _isOnPass = value;
                    // Обновляем значение в сервисе, чтобы команда BF использовала актуальное состояние
                    _serialPortService.IsOnPass = value;  // если вы реализовали аналогичное свойство в сервисе
                    OnPropertyChanged(nameof(IsOnPass));
                }
            }
        }

        public ICommand StartStopCommand { get; set; }

        public MainViewModel()
        {
            AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames());
            BaudRates = new ObservableCollection<int>(new int[] { 9600, 19200, 38400, 57600, 115200 });
            Parities = new ObservableCollection<string>(Enum.GetNames(typeof(Parity)));

            SelectedPort = AvailablePorts.Any() ? AvailablePorts.First() : "COM1";
            SelectedBaud = 9600;
            SelectedParity = Parity.None.ToString();
            Weight = 0.5;
            FixedAddress = 20; // по умолчанию

            StartStopCommand = new RelayCommand(ExecuteStartStop);

            _serialPortService = new SerialPortService();
            _serialPortService.LogMessage += AppendLog;
            _serialPortService.ErrorMessage += AppendLog;
            _serialPortService.HandshakeCompleted += () =>
            {
                AppendLog("Соединение установлено.");
                // Здесь можно вызывать диалог через сервис, если требуется
            };
        }

        private void ExecuteStartStop(object parameter)
        {
            if (!_isConnected)
            {
                try
                {
                    _serialPortService.Start(SelectedPort, SelectedBaud,
                        (Parity)Enum.Parse(typeof(Parity), SelectedParity), FixedAddress, Weight,
                        IsUspokoenie, IsPereg, Weight); // Передаём начальный вес; остальные параметры для формирования ответа могут обновляться внутри сервиса
                    _isConnected = true;
                    StartStopButtonText = "Стоп";
                    AppendLog("Порт открыт.");
                }
                catch (Exception ex)
                {
                    AppendLog("Ошибка: " + ex.Message);
                }
            }
            else
            {
                _serialPortService.Stop();
                _isConnected = false;
                StartStopButtonText = "Старт";
                AppendLog("Порт закрыт.");
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
