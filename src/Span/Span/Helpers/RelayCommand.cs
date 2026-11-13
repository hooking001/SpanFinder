using System;
using System.Windows.Input;

namespace Span.Helpers
{
    /// <summary>
    /// 파라미터 없는 단순 ICommand 구현. 항상 실행 가능.
    /// MVVM 커맨드 바인딩에서 사용된다.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute ?? throw new ArgumentNullException(nameof(execute));

        // CanExecute is constant; no change notifications are raised.
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

    /// <summary>
    /// 제네릭 파라미터를 받는 ICommand 구현. CanExecute 조건부 활성화 가능.
    /// </summary>
    /// <typeparam name="T">커맨드 파라미터 타입.</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T>? _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            if (parameter is T value) return _canExecute == null || _canExecute(value);
            return parameter is null && default(T) is null && (_canExecute == null || _canExecute(default!));
        }

        public void Execute(object? parameter)
        {
            if (parameter is T value) _execute(value);
            else if (parameter is null && default(T) is null) _execute(default!);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
