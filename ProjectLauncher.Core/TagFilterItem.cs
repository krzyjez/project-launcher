using System.ComponentModel;

namespace ProjectLauncher.Core;

public sealed class TagFilterItem : INotifyPropertyChanged
{
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public TagFilterItem(string name, Action selectionChanged)
    {
        Name = name;
        _selectionChanged = selectionChanged;
    }

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            _selectionChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
