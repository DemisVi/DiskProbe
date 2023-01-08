using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DiskProbe.Commands
{
    internal class Command : ICommand
    {
        public Command(Action<object?> execute, Predicate<object?> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        private readonly Predicate<object?> _canExecute;
        private readonly Action<object?> _execute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute.Invoke(parameter);

        public void Execute(object? parameter) => _execute.Invoke(parameter);
    }
}
