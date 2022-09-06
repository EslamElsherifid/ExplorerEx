﻿using System;

namespace ExplorerEx.Database.Shared;

/// <summary>
/// 实体中的属性带有这个Attribute的才会被记录进数据库，默认不记录
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DbColumn : Attribute {
	/// <summary>
	/// 是否为主键
	/// </summary>
	public bool IsPrimaryKey { get; set; }

	/// <summary>
	/// 是否自增
	/// </summary>
	public bool IsIdentity { get; set; }

	/// <summary>
	/// 指定存储时的列名
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// 存储时的最大长度
	/// </summary>
	public int MaxLength { get; set; } = -1;
}