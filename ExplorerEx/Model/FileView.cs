﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using ExplorerEx.Annotations;
using ExplorerEx.Database.Shared;
using ExplorerEx.Model.Enums;
using ExplorerEx.Utils;
using HandyControl.Tools.Extension;

namespace ExplorerEx.Model;

/// <summary>
/// 文件视图类型
/// </summary>
public enum FileViewType {
	/// <summary>
	/// 图标，表现为WarpPanel，每个小格上边是缩略图下边是文件名
	/// </summary>
	Icons,
	/// <summary>
	/// 列表，表现为WarpPanel，每个小格左边是缩略图右边是文件名
	/// </summary>
	List,
	/// <summary>
	/// 详细信息，表现为DataGrid，上面有Header，一列一列的
	/// </summary>
	Details,
	/// <summary>
	/// 平铺，表现为WarpPanel，每个小格左边是缩略图右边从上到下依次是文件名、文件类型描述、文件大小
	/// </summary>
	Tiles,
	/// <summary>
	/// 内容，表现为DataGrid，但Header不可见，左边是图标、文件名、大小，右边是详细信息
	/// </summary>
	Content
}

/// <summary>
/// 选择详细信息中的显示列
/// </summary>
public enum DetailListType : byte {
	/// <summary>
	/// 名称
	/// </summary>
	Name,

	#region 主页

	/// <summary>
	/// 可用空间
	/// </summary>
	AvailableSpace,
	/// <summary>
	/// 总大小
	/// </summary>
	TotalSpace,
	/// <summary>
	/// 文件系统（NTFS、FAT）
	/// </summary>
	FileSystem,
	/// <summary>
	/// 填充的百分比
	/// </summary>
	FillRatio,

	#endregion

	#region 回收站

	/// <summary>
	/// 原位置
	/// </summary>
	OriginalLocation,
	/// <summary>
	/// 删除日期
	/// </summary>
	DateDeleted,

	#endregion

	#region 搜索

	/// <summary>
	/// 所在文件夹
	/// </summary>
	FullPath,

	#endregion

	/// <summary>
	/// 修改日期
	/// </summary>
	DateModified,
	/// <summary>
	/// 类型
	/// </summary>
	Type,
	/// <summary>
	/// 文件大小
	/// </summary>
	FileSize,
	/// <summary>
	/// 创建日期
	/// </summary>
	DateCreated
}

/// <summary>
/// 详细信息中的一列，包括列的类型和宽度
/// </summary>
public class DetailList : IByteCodec {
	public DetailListType ListType { get; set; }

	/// <summary>
	/// 列宽，小于0表示自动宽度
	/// </summary>
	public double Width { get; set; }

	private static readonly DetailList[] DefaultHomeDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.Type, 100),
		new(DetailListType.AvailableSpace, 100),
		new(DetailListType.TotalSpace, 100),
		new(DetailListType.FillRatio, 150),
		new(DetailListType.FileSystem, 100)
	};

	private static readonly DetailList[] DefaultLocalFolderDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.DateModified, 200),
		new(DetailListType.Type, 100),
		new(DetailListType.FileSize, 100)
	};

	private static readonly DetailList[] DefaultRecycleBinDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.OriginalLocation, 300),
		new(DetailListType.DateDeleted, 200),
		new(DetailListType.FileSize, 100),
		new(DetailListType.Type, 100),
		new(DetailListType.DateModified, 200)
	};

	private static readonly DetailList[] DefaultSearchDetailLists = {
		new(DetailListType.Name, 300),
		new(DetailListType.DateModified, 200),
		new(DetailListType.Type, 100),
		new(DetailListType.FileSize, 100),
		new(DetailListType.FullPath, 800)
	};

	public static DetailList[] GetDefaultLists(PathType pathType) {
		return pathType switch {
			PathType.Home => DefaultHomeDetailLists,
			PathType.LocalFolder => DefaultLocalFolderDetailLists,
			PathType.RecycleBin => DefaultRecycleBinDetailLists,
			PathType.Search => DefaultSearchDetailLists,
			_ => DefaultLocalFolderDetailLists
		};
	}

	public DetailList() { }

	public DetailList(DetailListType listType, double width) {
		ListType = listType;
		Width = width;
	}

	internal void Deconstruct(out DetailListType listType, out double width) {
		listType = ListType;
		width = Width;
	}

	public int Length => sizeof(DetailListType) + sizeof(double);

	public void Encode(Span<byte> buf) {
		buf[0] = (byte)ListType;
		var width = BitConverter.GetBytes(Width);
		buf[1] = width[0];
		buf[2] = width[1];
	}

	public void Decode(ReadOnlySpan<byte> buf) {
		ListType = (DetailListType)buf[0];
		Width = BitConverter.ToDouble(buf.Slice(1, 2));
	}
}

/// <summary>
/// 当前路径的类型
/// </summary>
public enum PathType {
	Unknown,
	/// <summary>
	/// 首页，“此电脑”
	/// </summary>
	Home,
	/// <summary>
	/// 本地文件夹
	/// </summary>
	LocalFolder,
	/// <summary>
	/// 本地文件，仅在传参时使用
	/// </summary>
	LocalFile,
	/// <summary>
	/// 回收站
	/// </summary>
	RecycleBin,
	/// <summary>
	/// 搜索文件的结果
	/// </summary>
	Search,
	/// <summary>
	/// 网络驱动器
	/// </summary>
	NetworkDisk,
	OneDrive,
	/// <summary>
	/// 在一个压缩文件里
	/// </summary>
	Zip,
	Other
}

/// <summary>
/// 记录一个文件夹的视图状态，即排序方式、分组依据和查看类型，还包括详细信息的列的项目大小
/// </summary>
[Serializable]
[DbTable(TableName = "FolderViewDbSet")]
public class FileView : INotifyPropertyChanged {
	private readonly HashSet<string> changedPropertiesName = new();

	[DbColumn(IsPrimaryKey = true, MaxLength = 260)]  // 260是MAX_PATH
	public string? FullPath {
		get => fullPath;
		set {
			if (fullPath != value) {
				fullPath = value;
				StageChange();
			}
		}
	}
	private string? fullPath;

	[DbColumn]
	public PathType PathType {
		get => pathType;
		set {
			if (pathType != value) {
				pathType = value;
				StageChange();
			}
		}
	}
	private PathType pathType;

	[DbColumn]
	public DetailListType SortBy {
		get => sortBy;
		set {
			if (sortBy != value) {
				sortBy = value;
				StageChange();
				UpdateUI(nameof(SortByIndex));
			}
		}
	}
	private DetailListType sortBy;

	public ViewSortGroup SortByIndex => SortBy switch {
		DetailListType.Name => ViewSortGroup.SortByName,
		DetailListType.DateModified => ViewSortGroup.SortByDateModified,
		DetailListType.Type => ViewSortGroup.SortByType,
		DetailListType.FileSize => ViewSortGroup.SortByFileSize,
		_ => 0
	};

	[DbColumn]
	public bool IsAscending {
		get => isAscending;
		set {
			if (isAscending != value) {
				isAscending = value;
				StageChange();
				UpdateUI();
			}
		}
	}
	private bool isAscending;

	public DetailListType? GroupBy {
		get => groupBy;
		set {
			if (groupBy != value) {
				groupBy = value;
				UpdateUI();
				UpdateUI(nameof(GroupByIndex));
			}
		}
	}
	private DetailListType? groupBy;

	public ViewSortGroup GroupByIndex => GroupBy switch {
		DetailListType.Name => ViewSortGroup.GroupByName,
		DetailListType.DateModified => ViewSortGroup.GroupByDateModified,
		DetailListType.Type => ViewSortGroup.GroupByType,
		DetailListType.FileSize => ViewSortGroup.GroupByFileSize,
		_ => ViewSortGroup.GroupByNone
	};

	[DbColumn]
	public FileViewType FileViewType {
		get => fileViewType;
		set {
			if (fileViewType != value) {
				fileViewType = value;
				StageChange();
				UpdateUI(nameof(FileViewTypeIndex));
			}
		}
	}
	private FileViewType fileViewType;

	/// <summary>
	/// 用于绑定到下拉按钮
	/// </summary>
	public ViewSortGroup FileViewTypeIndex => FileViewType switch {
		FileViewType.Icons when ItemWidth > 160d && ItemHeight > 200d => ViewSortGroup.LargeIcons,
		FileViewType.Icons when ItemWidth > 100d && ItemHeight > 130d => ViewSortGroup.MediumIcons,
		FileViewType.Icons => ViewSortGroup.SmallIcons,
		FileViewType.List => ViewSortGroup.List,
		FileViewType.Details => ViewSortGroup.Details,
		FileViewType.Tiles => ViewSortGroup.Tiles,
		FileViewType.Content => ViewSortGroup.Content,
		_ => ViewSortGroup.Details
	};

	public Size ItemSize {
		get => new(ItemWidth <= 0 ? double.NaN : ItemWidth, ItemHeight <= 0 ? double.NaN : ItemHeight);
		set {
			if (double.IsNaN(value.Width)) {
				value.Width = 0;
			}
			if (double.IsNaN(value.Height)) {
				value.Height = 0;
			}
			if (!ItemWidth.Equals(value.Width) || !ItemHeight.Equals(value.Height)) {
				ItemWidth = value.Width;
				ItemHeight = value.Height;
				StageChange();
				StageChange(nameof(FileViewTypeIndex));
			}
		}
	}

	[DbColumn]
	public double ItemWidth { get; set; }

	[DbColumn]
	public double ItemHeight { get; set; }

	public List<DetailList>? DetailLists {
		get => DecodeData();
		set {
			EncodeData(value);
			StageChange();
		}
	}

	public byte[]? DetailListsData { get; set; }

	private void EncodeData(IReadOnlyCollection<DetailList>? detailLists) {
		if (detailLists == null || detailLists.Count == 0) {
			DetailListsData = null;
		} else {
			DetailListsData = new byte[detailLists.Sum(l => l.Length)];
			var index = 0;
			foreach (var detailList in detailLists) {
				detailList.Encode(DetailListsData.AsSpan(index, detailList.Length));
				index += detailList.Length;
			}
		}
	}

	private List<DetailList>? DecodeData() {
		if (DetailListsData == null || DetailListsData.Length == 0) {
			return null;
		}
		var detailLists = new List<DetailList>();
		var index = 0;
		while (index < DetailListsData.Length) {
			var list = new DetailList();
			list.Decode(DetailListsData.AsSpan(index, list.Length));
			detailLists.Add(list);
			index += list.Length;
		}
		return detailLists;
	}

	public void CommitChange() {
		lock (changedPropertiesName) {
			foreach (var changedName in changedPropertiesName) {
				UpdateUI(changedName);
			}
			changedPropertiesName.Clear();
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	[NotifyPropertyChangedInvocator]
	protected virtual void UpdateUI([CallerMemberName] string propertyName = null!) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public bool StageChangeEnabled { get; init; }

	/// <summary>
	/// 暂存更改
	/// </summary>
	/// <param name="propertyName"></param>
	private void StageChange([CallerMemberName] string propertyName = null!) {
		if (StageChangeEnabled) {
			lock (changedPropertiesName) {
				changedPropertiesName.Add(propertyName);
			}
		}
	}

	/// <summary>
	/// 将所有属性标记为已更改
	/// </summary>
	public void StageAllChanges() {
		if (StageChangeEnabled) {
			StageChange(nameof(FullPath));
			StageChange(nameof(PathType));
			StageChange(nameof(SortBy));
			StageChange(nameof(IsAscending));
			StageChange(nameof(GroupBy));
			StageChange(nameof(FileViewType));
			StageChange(nameof(ItemSize));
			StageChange(nameof(DetailListsData));
		}
	}

	public class SortByComparer : IComparer {
		public bool IsAscending { get; }

		public DetailListType SortBy { get; }

		public SortByComparer(DetailListType sortBy, bool isAscending) {
			SortBy = sortBy;
			IsAscending = isAscending;
		}

		private int InternalCompare(object? x, object? y) {
			if (x is FileListViewItem i1 && y is FileListViewItem i2) {
				if (i1.IsFolder != i2.IsFolder) {
					return i1.IsFolder ? -1 : 1;
				}
				return SortBy switch {
					DetailListType.Type => string.Compare(i1.Type, i2.Type, StringComparison.Ordinal),
					DetailListType.FileSize => i1.FileSize.CompareTo(i2.FileSize),
					DetailListType.DateModified => i1 is FileSystemItem f1 && i2 is FileSystemItem f2 ? f1.DateModified.CompareTo(f2.DateModified) : string.Compare(i1.Name, i2.Name, StringComparison.OrdinalIgnoreCase),
					_ => string.Compare(i1.Name, i2.Name, StringComparison.OrdinalIgnoreCase),
				};
			}
			throw new NotImplementedException();
		}

		public int Compare(object? x, object? y) {
			return IsAscending ? InternalCompare(x, y) : -InternalCompare(x, y);
		}
	}
}