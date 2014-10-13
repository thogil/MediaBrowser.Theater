﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Theater.Api.Navigation;
using MediaBrowser.Theater.Api.Session;
using MediaBrowser.Theater.Api.UserInterface;
using MediaBrowser.Theater.DefaultTheme.Home.ViewModels;
using MediaBrowser.Theater.Presentation.Controls;
using MediaBrowser.Theater.Presentation.ViewModels;

namespace MediaBrowser.Theater.DefaultTheme.ItemDetails.ViewModels
{
    public class PeopleListViewModel
        : BaseViewModel, IItemDetailSection, IKnownSize
    {
        private readonly IImageManager _imageManager;
        private readonly BaseItemDto _item;
        private readonly IConnectionManager _connectionManager;
        private readonly INavigator _navigator;
        private readonly ISessionManager _sessionManager;

        public PeopleListViewModel(BaseItemDto item, IConnectionManager connectionManager, ISessionManager sessionManager, IImageManager imageManager, INavigator navigator)
        {
            _item = item;
            _connectionManager = connectionManager;
            _sessionManager = sessionManager;
            _imageManager = imageManager;
            _navigator = navigator;

            People = new RangeObservableCollection<IViewModel>();
            LoadItems();
        }

        public RangeObservableCollection<IViewModel> People { get; private set; }

        public int SortOrder
        {
            get { return 4; }
        }

        public Size Size
        {
            get
            {
                if (People.Count == 0) {
                    return new Size(0, 0);
                }

                int width = Math.Min(People.Count, 3);

                return new Size((167 + 2 * HomeViewModel.TileMargin) * width + 20, 700);
            }
        }

        private void LoadItems()
        {
            IEnumerable<IViewModel> items = _item.People.Select(p => new PersonListItemViewModel(p, _imageManager, _sessionManager, _navigator));

            People.Clear();
            People.AddRange(items);
        }
    }

    public class PersonListItemViewModel
        : BaseViewModel, IDisposable
    {
        private readonly IImageManager _imageManager;
        private readonly ISessionManager _sessionManager;
        private readonly INavigator _navigator;
        private readonly BaseItemPerson _person;

        private Image _image;
        private CancellationTokenSource _imageCancellationTokenSource;

        public PersonListItemViewModel(BaseItemPerson person, IImageManager imageManager, ISessionManager sessionManager, INavigator navigator)
        {
            _person = person;
            _imageManager = imageManager;
            _sessionManager = sessionManager;
            _navigator = navigator;

            NavigateCommand = new RelayCommand(arg => NavigateToPerson());
        }

        private async void NavigateToPerson()
        {
            var apiClient = _sessionManager.ActiveApiClient;
            var person = await apiClient.GetPersonAsync(_person.Name, _sessionManager.CurrentUser.Id);
            await _navigator.Navigate(Go.To.Item(person));
        }

        public string Name
        {
            get { return _person.Name; }
        }

        public string Role
        {
            get { return _person.Role; }
        }

        public Image Artwork
        {
            get
            {
                Image img = _image;

                CancellationTokenSource tokenSource = _imageCancellationTokenSource;

                if (img == null && (tokenSource == null || tokenSource.IsCancellationRequested)) {
                    DownloadImage();
                }

                return _image;
            }

            private set
            {
                bool changed = !Equals(_image, value);

                _image = value;

                if (value == null) {
                    CancellationTokenSource tokenSource = _imageCancellationTokenSource;

                    if (tokenSource != null) {
                        tokenSource.Cancel();
                    }
                }

                if (changed) {
                    OnPropertyChanged();
                }
            }
        }

        public ICommand NavigateCommand { get; private set; }

        public void Dispose()
        {
            DisposeCancellationTokenSource();
        }

        private async void DownloadImage()
        {
            _imageCancellationTokenSource = new CancellationTokenSource();

            if (!string.IsNullOrEmpty(_person.PrimaryImageTag)) {
                var options = new ImageOptions {
                    Height = 250,
                    Width = 167,
                    ImageType = ImageType.Primary,
                    Tag = _person.PrimaryImageTag
                };

                var apiClient = _sessionManager.ActiveApiClient;
                Artwork = await _imageManager.GetRemoteImageAsync(apiClient.GetPersonImageUrl(_person, options), _imageCancellationTokenSource.Token);
            }
        }

        private void DisposeCancellationTokenSource()
        {
            if (_imageCancellationTokenSource != null) {
                _imageCancellationTokenSource.Cancel();
                _imageCancellationTokenSource.Dispose();
                _imageCancellationTokenSource = null;
            }
        }
    }

    public class PeopleListSectionGenerator
        : IItemDetailSectionGenerator
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IImageManager _imageManager;
        private readonly INavigator _navigator;
        private readonly ISessionManager _sessionManager;

        public PeopleListSectionGenerator(IConnectionManager connectionManager, IImageManager imageManager, INavigator navigator, ISessionManager sessionManager)
        {
            _connectionManager = connectionManager;
            _imageManager = imageManager;
            _navigator = navigator;
            _sessionManager = sessionManager;
        }

        public bool HasSection(BaseItemDto item)
        {
            return item != null && item.People != null && item.People.Length > 0;
        }

        public Task<IEnumerable<IItemDetailSection>> GetSections(BaseItemDto item)
        {
            IItemDetailSection section = new PeopleListViewModel(item, _connectionManager, _sessionManager, _imageManager, _navigator);
            return Task.FromResult<IEnumerable<IItemDetailSection>>(new[] { section });
        }
    }
}