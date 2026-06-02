using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace get_link_manga
{
    public class GalleryItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private string _link;
        private string _name;
        private int _originalIndex;
        private int _linkCount;
        private bool _isDuplicate;
        private bool _hasNoChapters;

        public bool HasNoChapters
        {
            get => _hasNoChapters;
            set
            {
                if (_hasNoChapters != value)
                {
                    _hasNoChapters = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDuplicate
        {
            get => _isDuplicate;
            set
            {
                if (_isDuplicate != value)
                {
                    _isDuplicate = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Link
        {
            get => _link;
            set
            {
                if (_link != value)
                {
                    _link = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int OriginalIndex
        {
            get => _originalIndex;
            set
            {
                if (_originalIndex != value)
                {
                    _originalIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public int LinkCount
        {
            get => _linkCount;
            set
            {
                if (_linkCount != value)
                {
                    _linkCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
