using System;
using System.Threading.Tasks;
using Dapper;
using MySqlConnector;

namespace LiteVip;

public class VipTest
{
    private readonly string _dbConnectionString;

    public VipTest(string dbConnectionString)
    {
        _dbConnectionString = dbConnectionString;
    }

    public async Task AddUserToVipTest(string steamId, long count, long endTime)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var insertUserQuery = @"
            INSERT INTO `litevip_test` (`SteamId`, `Count`, `EndTime`)
            VALUES (@SteamId, @Count, @EndTime);";

            await dbConnection.ExecuteAsync(insertUserQuery,
                new { SteamId = steamId, Count = count, EndTime = endTime });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    public async Task<bool> IsUserInVipTest(string steamId)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var checkUserQuery = @"
            SELECT COUNT(*)
            FROM `litevip_test`
            WHERE `SteamId` = @SteamId;";

            var count = dbConnection.ExecuteScalarAsync<int>(checkUserQuery, new { SteamId = steamId }).Result;

            return count > 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }
    
    public async Task UpdateUserVipTestCount(string steamId, long additionalCount)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var updateCountQuery = @"
            UPDATE `litevip_test`
            SET `Count` = `Count` + @AdditionalCount
            WHERE `SteamId` = @SteamId;";

            await dbConnection.ExecuteAsync(updateCountQuery, new { SteamId = steamId, AdditionalCount = additionalCount });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task<long?> GetVipTestCount(string steamId)
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var selectCountQuery = @"
            SELECT `Count` FROM `litevip_test` WHERE `SteamId` = @SteamId;";

            return await dbConnection.ExecuteScalarAsync<long?>(selectCountQuery, new { SteamId = steamId });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
}
