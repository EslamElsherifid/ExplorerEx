﻿using System;
using System.Windows.Controls;
using System.Windows.Input;
using ExplorerEx.Model;
using ExplorerEx.Utils;
using ExplorerEx.ViewModel;
using static ExplorerEx.View.Controls.FileDataGrid;
using hc = HandyControl.Controls;
using TextBox = HandyControl.Controls.TextBox;

namespace ExplorerEx.View.Controls; 

public partial class FileViewGrid {
	/// <summary>
	/// 创建文件或文件夹
	/// </summary>
	public SimpleCommand CreateCommand { get; }

	private FileViewGridViewModel ViewModel => (FileViewGridViewModel)DataContext;

	public FileViewGrid() {
		CreateCommand = new SimpleCommand(e => {
			if (e is MenuItem menuItem) {
				Create((CreateFileItem)menuItem.DataContext);
			}
		});

		InitializeComponent();
	}

	/// <summary>
	/// 创建文件或文件夹
	/// </summary>
	public void Create(CreateFileItem item) {
		var viewModel = ViewModel;
		if (viewModel.PathType == PathTypes.Home) {
			return;
		}
		try {
			FileDataGrid.StartRename(item.Create(viewModel.FullPath));
		} catch (Exception e) {
			hc.MessageBox.Error(e.Message, "Cannot_create".L());
		}
	}

	private async void AddressBar_OnKeyDown(object sender, KeyEventArgs e) {
		switch (e.Key) {
		case Key.Enter:
			await ViewModel.LoadDirectoryAsync(((TextBox)sender).Text);
			break;
		}
	}
}