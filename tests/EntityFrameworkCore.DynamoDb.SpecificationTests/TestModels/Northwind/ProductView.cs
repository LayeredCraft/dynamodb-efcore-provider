// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;

#nullable disable

public class ProductView
{
    public int ProductID { get; set; }
    public string ProductName { get; set; }
    public string CategoryName { get; set; }
}
