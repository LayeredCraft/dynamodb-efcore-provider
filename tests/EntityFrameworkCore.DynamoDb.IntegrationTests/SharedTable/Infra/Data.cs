using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

public static class SharedItems
{
    public static readonly List<UserEntity> Users =
    [
        new() { Pk = "TENANT#U", Sk = "USER#1", Name = "Ada" },
        new() { Pk = "TENANT#U", Sk = "USER#2", Name = "Lin" },
    ];

    public static readonly List<OrderEntity> Orders =
    [
        new() { Pk = "TENANT#O", Sk = "ORDER#1", Description = "order-one" },
        new() { Pk = "TENANT#O", Sk = "ORDER#2", Description = "order-two" },
    ];

    public static readonly List<EmployeeEntity> Employees =
    [
        new()
        {
            Pk = "TENANT#H", Sk = "PERSON#EMP-1", Name = "Eve", Department = "Engineering",
        },
    ];

    public static readonly List<ManagerEntity> Managers =
    [
        new() { Pk = "TENANT#H", Sk = "PERSON#MGR-1", Name = "Max", Level = 7 },
    ];

    public static readonly IReadOnlyList<Dictionary<string, AttributeValue>> AttributeValues =
    [
        ..UserEntityMapper.ToItems(Users),
        ..OrderEntityMapper.ToItems(Orders),
        ..EmployeeEntityMapper.ToItems(Employees),
        ..ManagerEntityMapper.ToItems(Managers),
    ];
}
