﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Archive.ClientBase.Messages;
using Archive.ClientBase.Models;
using Archive.ClientBase.Services;
using Archive.ClientBase.Validation;
using Archive.Contracts;

namespace Archive.ClientBase.ViewModel
{
	public partial class ArchiveEntryViewModel : AbstractViewModel
	{
		private bool _isBusy;
		private ArchiveEntryModel _item;


		private readonly Func<ArchiveGroupViewModel> _archiveGroupViewModelFactory;
		private readonly IErrorService _errorService;
		private readonly INavigationService _navigationService;
		private readonly IMessenger _messenger;

		public ArchiveEntryModel Item
		{
			get => _item;
			set
			{
				Set(nameof(Item), ref _item, value);

				if (Item.Groups == null)
				{
					Groups = new ObservableCollection<ArchiveGroupViewModel>();
				}
				else
				{
					Groups = new ObservableCollection<ArchiveGroupViewModel>(Item.Groups.Select(e =>
					{
						var model = new ArchiveGroupModel()
						{
							Id = e.Id,
							GroupName = e.GroupName,
						};

						var viewModel = _archiveGroupViewModelFactory();
						viewModel.ParentId = Item.Id;
						viewModel.Item = model;
						return viewModel;
					}));
				}
			}
		}

		public bool IsBusy { get => _isBusy; private set => Set(nameof(IsBusy), ref _isBusy, value); }

		public ArchiveEntryViewModel(
			Func<ArchiveGroupViewModel> archiveGroupViewModelFactory,
			IErrorService errorService,
			INavigationService navigationService,
			IMessenger messenger)
		{
			_archiveGroupViewModelFactory = archiveGroupViewModelFactory;

			_errorService = errorService;
			_navigationService = navigationService;
			_messenger = messenger;

			SaveCommand = new AsyncCommand(SaveAsync, DisplayError);
			DeleteCommand = new AsyncCommand(DeleteAsync, DisplayError);
			EditArchiveEntryCommand = new AsyncCommand(EditArchiveEntryAsync, DisplayError, () => !IsBusy);
		}

		private async Task DisplayError(Task task)
		{
			await _errorService.ShowAlertAsync("Error...", task.Exception);
		}

		public void Handle(ArchiveEntryUpdated obj)
		{
			if (Item.Id != obj.Id)
			{
				return;
			}

			Item = new ArchiveEntryModel()
			{
				Id = obj.Id,
				Date = obj.Date,
				Description = obj.Description,
				Groups = obj.Groups,
				OriginalName = obj.OriginalName,
				IsFolder = obj.IsFolder,
				FileSize = obj.FileSize,
			};

			Groups = new ObservableCollection<ArchiveGroupViewModel>(Item.Groups.Select(e =>
			{
				var model = new ArchiveGroupModel()
				{
					Id = e.Id,
					GroupName = e.GroupName,
				};

				var viewModel = _archiveGroupViewModelFactory();
				viewModel.ParentId = Item.Id;
				viewModel.Item = model;
				return viewModel;
			}));
		}

		public AsyncCommand SaveCommand { get; }
		public async Task SaveAsync()
		{
			if (IsBusy)
			{
				return;
			}

			try
			{
				IsBusy = true;

				Item.Groups = Groups.Select(e => new ArchiveGroup()
				{
					Id = e.Item.Id,
					GroupName = e.Item.GroupName,
				}).ToList();

				var errors = Item.Errors
					.Where(err => err.Severity > ValidationSeverity.Info)
					.Select(e => e.Text);

				if (errors.Any())
				{
					var message = "There are some validation errors:";
					message += Environment.NewLine + Environment.NewLine;
					message += string.Join(Environment.NewLine, errors);
					message += Environment.NewLine + Environment.NewLine;
					message += "Do you want to continue?";

					var vm = new YesNoPageViewModel()
					{
						Title = "Validation",
						Message = message
					};

					var selection = await _navigationService.RequestInputAsync(vm);

					if (!selection)
					{
						return;
					}
				}

				await _navigationService.NavigateBack();
				await _messenger.SendAsync(new SaveArchiveEntry(Item));
			}
			catch (Exception ex)
			{
				await _errorService.ShowAlertAsync("Error saving...", ex);
			}
			finally
			{
				IsBusy = false;
			}
		}

		public AsyncCommand DeleteCommand { get; }
		public async Task DeleteAsync()
		{
			if (IsBusy)
			{
				return;
			}

			try
			{
				IsBusy = true;

				var vm = new YesNoPageViewModel()
				{
					Title = "Delete?",
					Message = "Should the item be deleted?"
				};

				var selection = await _navigationService.RequestInputAsync(vm);

				if (selection)
				{
					await _navigationService.NavigateBack();
					await _messenger.SendAsync(new DeleteArchiveEntry(Item.Id));
				}
			}
			catch (Exception ex)
			{
				await _errorService.ShowAlertAsync("Error deleting...", ex);
			}
			finally
			{
				IsBusy = false;
			}
		}

		public AsyncCommand EditArchiveEntryCommand { get; }
		public async Task EditArchiveEntryAsync()
		{
			if (IsBusy)
			{
				return;
			}

			try
			{
				IsBusy = true;
				await _messenger.SendAsync(new EditArchiveEntry(Item.Id));
			}
			catch (Exception ex)
			{
				await _errorService.ShowAlertAsync("Error starting edit...", ex);
			}
			finally
			{
				IsBusy = false;
			}
		}
	}
}