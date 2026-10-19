using Dapper;
using FMSISSTransactionService.Helpers;
//using FMSISSTransactionService.Models;
using FMSMyService.Helpers;
using FMSMyService.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;


namespace FMSMyService.Services
{
    public class CommonMethods
    {
        public class CommonMethods
        {
            public IConfiguration _Configurations { get; set; }

            private TransactionValidator _validator;
            private readonly SemaphoreSlim _validatorLock = new(1, 1);
            private OfflineGeoCoder _OfflineGeoCoder = new OfflineGeoCoder();
            public Decryption _EncryptDecryptString { get; set; } = new Decryption();

            public string _DefaultConnections { get; set; }
            public string _OracleConnection { get; set; }
            public string _Schema { get; set; }
            public string _LocalSchema { get; set; }
            public string _LocalHistorySchema { get; set; }
            public string _LocalFailedSchema { get; set; }
            public string _TranCount { get; set; }
            public string LastCutOffTime { get; set; }
            public bool _QueryFetchByTranCount { get; set; }
            public bool _ApplyTrim { get; set; }
            public double _RecheckTimeout { get; set; } = 90;
            public string _recheckTrxLogidCSV { get; set; }
            private async Task<TransactionValidator> GetOrCreateValidatorAsync()
            {
                if (_validator != null) return _validator;

                await _validatorLock.WaitAsync();
                try
                {
                    _validator ??= await TransactionValidator.CreateAsync(_DefaultConnections!, string.IsNullOrWhiteSpace(_LocalSchema) ? "BOP_Issuance" : _LocalSchema, applyTrim: _ApplyTrim);
                    _validator.ApplyTrim = _ApplyTrim;
                    return _validator;
                }
                finally
                {
                    _validatorLock.Release();
                }
            }

            public CommonMethods(IConfiguration configuration)
            {
                _Configurations = configuration;
                _DefaultConnections = _EncryptDecryptString.DecryptString(_Configurations.GetValue<string>("ConnectionStrings:DefaultConnection"));
                _OracleConnection = _EncryptDecryptString.DecryptString(_Configurations.GetValue<string>("ConnectionStrings:OracleConnection"));

                _Schema = _EncryptDecryptString.DecryptString(_Configurations.GetValue<string>("TransactionSchemas:SourceSchema"));
                _LocalSchema = _EncryptDecryptString.DecryptString(_Configurations.GetValue<string>("TransactionSchemas:LocalSchema"));
                _LocalHistorySchema = _EncryptDecryptString.DecryptString(_Configurations.GetValue<string>("TransactionSchemas:LocalSixMonthsSchema"));
                _LocalFailedSchema = _EncryptDecryptString.DecryptString(_Configurations.GetValue<string>("TransactionSchemas:FailedTransactionSchema"));

                _TranCount = _EncryptDecryptString.DecryptString(_Configurations.GetValue<string>("ServiceSettings:TranCount"));
                _QueryFetchByTranCount = _Configurations.GetValue<bool>("ServiceSettings:QueryFetchByTranCount");
                _ApplyTrim = _Configurations.GetValue<bool>("ServiceSettings:ApplyTrim");
                _RecheckTimeout = _Configurations.GetValue<double?>("ServiceSettings:RecheckTimeout") ?? 3;
            }

            public IEnumerable<Rules> GetRules()
            {
                try
                {
                    using IDbConnection db = new SqlConnection(_DefaultConnections);
                    string queryRules = @"SELECT RuleId, RuleName, Description, Status, Frequency, Days, Hours, Minutes, IsLookBack, IsCumulative, RuleType, OrderOfExecution, AutoClose, IsDeleted 
                                      FROM Rules 
                                      WHERE RuleType = 'ISS' AND Status = 'Active' AND IsDeleted = 0 
                                      ORDER BY OrderOfExecution DESC";

                    var rules = db.Query<Rules>(queryRules).ToList();
                    var criterias = CommonCriteria().ToList();

                    foreach (var rule in rules)
                    {
                        rule.Criterias = criterias
                            .Where(c => c.RuleId == rule.RuleId)
                            .Select(c => new Criterias
                            {
                                CriteriaId = c.CriteriaId,
                                RuleId = c.RuleId,
                                Parameter = c.ParameterId,
                                ParameterName = c.ParameterName,
                                FieldType = c.FieldType,
                                Operator = c.Operator,
                                Value = c.Value,
                                LogicalOperator = c.LogicalOperator,
                                CriteriaType = c.CriteriaType,
                                IsCumulative = c.IsCumulative
                            })
                            .ToList();
                    }

                    return rules;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "GetRules");
                    throw;
                }
            }

            public IEnumerable<RulesAndCriterias> CommonCriteria(long? ruleId = null)
            {
                try
                {
                    using IDbConnection db = new SqlConnection(_DefaultConnections);
                    string query = @"SELECT P.ParameterId, C.CriteriaId, P.FieldType, C.Operator, P.ParameterName, C.Value, C.RuleId, C.LogicalOperator, C.CriteriaType, C.IsCumulative 
                                 FROM Rules R 
                                 INNER JOIN Criterias C ON R.RuleId = C.RuleId 
                                 INNER JOIN Parameters P ON C.Parameter = P.ParameterId 
                                 WHERE R.IsDeleted = 0 AND R.Status = 'Active' AND R.RuleType = 'ISS' AND C.IsDeleted = 0 AND P.IsDeleted = 0";

                    if (ruleId.HasValue)
                    {
                        query += " AND C.RuleId = @RuleId";
                        return db.Query<RulesAndCriterias>(query, new { RuleId = ruleId.Value });
                    }

                    return db.Query<RulesAndCriterias>(query);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "CommonCriteria");
                    throw;
                }
            }

            public IEnumerable<Parameter> Get_Parameters()
            {
                try
                {
                    using IDbConnection db = new SqlConnection(_DefaultConnections);
                    string query = @"SELECT ParameterId, ParameterName, FieldType, IsDeleted, DataType  
                                 FROM Parameters 
                                WHERE DataType = 'ISS' AND IsDeleted = 0 AND ParameterId <> 253 ";
                    return db.Query<Parameter>(query);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Get_Parameters");
                    throw;
                }
            }

            public ModifyPoints GetModifyPoint()
            {
                try
                {
                    using IDbConnection db = new SqlConnection(_DefaultConnections);
                    string query = @"SELECT TrxLogId2, ModifiedOn, IsSeeded from ModifyPoints WHERE ModifyType = 'ISS'";
                    return db.QueryFirstOrDefault<ModifyPoints>(query);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "GetModifyPoint");
                    throw;
                }
            }

            public async Task UpdateModifyPointsAsync(string modifyType, DateTime modifiedOn, long trxLogId2)
            {
                try
                {
                    using var dbContext = new AppDbContext(_DefaultConnections);
                    var modifyPoint = await dbContext.ModifyPoints.FirstOrDefaultAsync(m => m.ModifyType == modifyType);
                    if (modifyPoint != null)
                    {
                        modifyPoint.ModifiedOn = modifiedOn;
                        modifyPoint.TrxLogId2 = trxLogId2;
                        modifyPoint.IsSeeded = true;
                    }
                    else
                    {
                        modifyPoint = new ModifyPoints
                        {
                            ModifyType = modifyType,
                            ModifiedOn = modifiedOn,
                            TrxLogId2 = trxLogId2,
                            IsSeeded = TransactionValidator
                        };
                        await dbContext.ModifyPoints.AddAsync(modifyPoint);
                    }
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "UpdateModifyPointsAsync");
                    throw;
                }
            }

            public void DeleteFutureTransactions(long TrxLogId)
            {
                try
                {
                    Logger.Info($"Deleting Transactions From {_LocalSchema} and {_LocalHistorySchema} > {TrxLogId}");

                    string sqlQuery = @$"DELETE FROM {_LocalSchema} WHERE TRANSACTION_LOG_ID > {TrxLogId};
                                        DELETE FROM {_LocalHistorySchema} WHERE TRANSACTION_LOG_ID > {TrxLogId};";

                    using (SqlConnection connection = new SqlConnection(_DefaultConnections))
                    {
                        connection.Open();
                        using (SqlCommand sqlCommand = new SqlCommand(sqlQuery, connection))
                        {
                            sqlCommand.CommandTimeout = 0;
                            var a = sqlCommand.ExecuteNonQuery();

                            Logger.Info($"Records Removed: {a}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "DeleteFutureTransactions");
                    throw;
                }
            }
            public string GetSelectParams(List<Parameter> Parameters)
            {
                string str = "";
                foreach (Parameter parameter in Parameters)
                {
                    if (parameter.ParameterName == "TIME_LOC_TRAN_DT")
                        str += "TO_CHAR(TIME_LOC_TRAN_DT, 'YYYY-MM-DD HH24:MI:SS') AS TIME_LOC_TRAN_DT, ";
                    else if (parameter.ParameterName == "DB_DATE_TIME")
                        str += "TO_CHAR(DB_DATE_TIME, 'YYYY-MM-DD HH24:MI:SS') AS DB_DATE_TIME, ";
                    else if (parameter.ParameterName == "DB_DATE_TIME_UPDATE")
                        str += "TO_CHAR(DB_DATE_TIME_UPDATE, 'YYYY-MM-DD HH24:MI:SS') AS DB_DATE_TIME_UPDATE, ";
                    else if (parameter.ParameterName == "TRANSM_DATE_TIME_DT")
                        str += "TO_CHAR(TRANSM_DATE_TIME_DT, 'YYYY-MM-DD HH24:MI:SS') AS TRANSM_DATE_TIME_DT, ";

                    else if (parameter.ParameterName == "FORWARD_INSTITUTION_ID_CODE")
                        str += "SUBSTR(FORWARD_INSTITUTION_ID_CODE, 0, 6) AS FORWARD_INSTITUTION_ID_CODE, ";
                    else if (parameter.ParameterName == "COMPANYID")
                        str += "NULL AS COMPANYID, ";
                    else if (parameter.ParameterName == "ACQ_INST_IMD")
                        str += "ACQ_INSTITUTION_ID_CODE AS ACQ_INST_IMD, ";
                    else if (parameter.ParameterName == "ATM_ID")
                        str += "CARD_ACCEPTOR_TERMINAL_ID AS ATM_ID, ";
                    else
                        str += parameter.ParameterName + ", ";
                }
                if (str.Length > 0)
                    str = str.Remove(str.Length - 2);
                return str;
            }

            public async Task<List<DataRow>> GetDataSetLatestTrans(long PrevTrxId, List<Parameter> Parameters)
            {
                LastCutOffTime = null;
                int tranLimit = Convert.ToInt32(_TranCount);
                int count = 0;
                string query = string.Empty;

                DataSet dataSet = new DataSet();
                List<DataRow> transactions;

                var ConcatedParameters = GetSelectParams(Parameters);

                try
                {
                    Logger.Info("Fetching Transactions From ISS Oracle Source Schema");

                    using (OracleConnection connection = new OracleConnection(_OracleConnection))
                    {
                        if (_QueryFetchByTranCount == true)
                        {
                            query = @$"SELECT {ConcatedParameters} FROM {_Schema} 
                                    WHERE TRANSACTION_LOG_ID > {PrevTrxId} AND PAN IS NOT NULL
                                    AND TIME_LOC_TRAN_DT IS NOT NULL AND SOURCE_CHANNEL_ID IN ('241','441','443','444',
                                    '446','447','448','560','581','620','640','680') AND PAN NOT LIKE '0000000000000'
                                    ORDER BY TRANSACTION_LOG_ID ASC FETCH FIRST {tranLimit} ROWS ONLY";
                        }
                        else
                        {
                            query = @$"SELECT {ConcatedParameters} FROM {_Schema} 
                                    WHERE TRANSACTION_LOG_ID > {PrevTrxId} AND PAN IS NOT NULL
                                    AND TIME_LOC_TRAN_DT IS NOT NULL AND SOURCE_CHANNEL_ID IN ('241','441','443','444',
                                    '446','447','448','560','581','620','640','680') AND PAN NOT LIKE '0000000000000'
                                    ORDER BY TRANSACTION_LOG_ID ASC";
                        }

                        await connection.OpenAsync();

                        Logger.Info($"Oracle Query Parameter TrxID > {PrevTrxId}");

                        using (OracleCommand selectCommand = new OracleCommand(query, connection))
                        {
                            selectCommand.CommandTimeout = 0;
                            selectCommand.CommandText = query;
                            using (OracleDataAdapter oracleDataAdapter = new OracleDataAdapter(selectCommand))
                                oracleDataAdapter.Fill(dataSet, "Transactions");
                        }

                        count = dataSet?.Tables.Count > 0 ? dataSet.Tables[0].Rows.Count : 0;

                        if (count > 0)
                        {
                            string lasttrxlogid = dataSet.Tables[0].AsEnumerable().Select(x => Convert.ToInt64(x["TRANSACTION_LOG_ID"])).DefaultIfEmpty(0L).Max().ToString();
                            Logger.Info($"Latest Transactions Fetched {count}");
                            Logger.Info($"Latest Transaction ID: {lasttrxlogid}");
                        }
                    }

                    transactions = dataSet.Tables[0].AsEnumerable().OrderBy(x => Convert.ToInt64(x["TRANSACTION_LOG_ID"])).Take(tranLimit).ToList();

                    // INSERT INTO SQL SERVER 
                    if (count > 0)
                    {
                        var result = await InsertIntoTempTable(transactions);
                        return result.ValidRows;
                    }

                    return transactions;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "GetDataSetLatestTrans");
                    throw;
                }
            }

            public async Task<TransactionValidator.ValidationResult> InsertIntoTempTable(IEnumerable<DataRow> data)
            {
                try
                {
                    if (data == null || !data.Any())
                    {
                        return new TransactionValidator.ValidationResult
                        {
                            ValidTable = new DataTable(),
                            FailedTable = new DataTable()
                        };
                    }

                    Logger.Info("Validating transactions dynamically before insert...");
                    var validator = await GetOrCreateValidatorAsync();
                    var result = validator.ValidateAndSplit(data);
                    Logger.Info($"Dynamic Validation Completed. Valid: {result.ValidCount}, Failed: {result.FailedCount}");

                    using SqlConnection sql = new SqlConnection(_DefaultConnections);
                    await sql.OpenAsync();

                    using SqlTransaction sqltran = sql.BeginTransaction();

                    try
                    {
                        if (result.ValidCount > 0 && ServiceRun._EnableDBAlteration)
                        {
                            using SqlBulkCopy bulkcopy = new SqlBulkCopy(sql, SqlBulkCopyOptions.Default, sqltran)
                            {
                                DestinationTableName = _LocalSchema,
                                BatchSize = 5000,
                                BulkCopyTimeout = 300
                            };

                            // Dynamically map all columns present in ValidTable
                            foreach (DataColumn col in result.ValidTable.Columns)
                            {
                                bulkcopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                            }

                            await bulkcopy.WriteToServerAsync(result.ValidTable);
                            Logger.Info($"Inserted Valid Rows Count: {result.ValidCount} -> {_LocalSchema}");
                        }

                        if (result.FailedCount > 0)
                        {
                            bool insertionstatus = await BulkInsertFailedRows(sql, sqltran, result.FailedTable);

                            if (insertionstatus == true)
                                Logger.Warning($"Inserted Failed Rows Count: {result.FailedCount} -> {_LocalFailedSchema}");
                            else
                                Logger.Warning($"Insertion Failed into {_LocalFailedSchema}");
                        }

                        sqltran.Commit();
                    }
                    catch (Exception ex)
                    {
                        sqltran.Rollback();
                        Logger.LogException(ex, "InsertIntoTempTable - BulkCopy");
                        throw;
                    }

                    try
                    {
                        if (ServiceRun._EnableDBAlteration)
                        {
                            using var con = new SqlConnection(_DefaultConnections);

                            string query = $@"
                        INSERT INTO {_LocalHistorySchema} 
                        SELECT * FROM {_LocalSchema} 
                        WHERE TRANSACTION_LOG_ID > ISNULL((SELECT MAX(TRANSACTION_LOG_ID) FROM {_LocalHistorySchema}), 0);";

                            int rowsCopied = await con.ExecuteAsync(
                                query,
                                param: null,
                                commandTimeout: 120
                            );

                            Logger.Info($"Inserted into History Rows Count  {rowsCopied}");
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "InsertIntoTempTable - HistoryCopy");
                        throw;
                    }

                    if (ServiceRun._EnableDBAlteration)
                    {
                        string updateSeededQuery = "UPDATE ModifyPoints SET IsSeeded = 1 WHERE ModifyType = 'ISS'";
                        await sql.ExecuteAsync(updateSeededQuery, commandTimeout: 30);
                        Logger.Info("Updated ModifyPoints IsSeeded = 1");
                    }


                    await PurgeFailedTransactionsAsync();

                    return result;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "InsertIntoTempTable");
                    throw;
                }
            }

            private async Task<bool> BulkInsertFailedRows(SqlConnection sql, SqlTransaction sqltran, DataTable failedTable)
            {
                try
                {
                    using SqlBulkCopy failedBulkCopy = new SqlBulkCopy(sql, SqlBulkCopyOptions.Default, sqltran)
                    {
                        DestinationTableName = _LocalFailedSchema,
                        BatchSize = 5000,
                        BulkCopyTimeout = 300
                    };

                    // Dynamically map all columns present in FailedTable
                    foreach (DataColumn col in failedTable.Columns)
                    {
                        failedBulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                    }

                    await failedBulkCopy.WriteToServerAsync(failedTable);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to Insert into {_LocalFailedSchema}, EX: {ex}");
                    return false;
                }

            }

            public async Task PurgeFailedTransactionsAsync(int retentionMonths = 6)
            {
                try
                {
                    using var con = new SqlConnection(_DefaultConnections);

                    string purgeQuery = $@"
                    DELETE FROM {_LocalFailedSchema}
                    WHERE FailedOn < DATEADD(MONTH, -@RetentionMonths, GETUTCDATE())";

                    int purgedRows = await con.ExecuteAsync(
                        purgeQuery,
                        new { RetentionMonths = retentionMonths },
                        commandTimeout: 60
                    );

                    if (purgedRows > 0)
                    {
                        Logger.Info($"Auto-Purged {purgedRows} expired records from {_LocalFailedSchema} (>{retentionMonths} months old)");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to purge {_LocalFailedSchema}: {ex.Message}");
                }
            }

            public int GenerateFrequencySuspect(long PreviousTrxId, string RECHECK = "")
            {
                int count = 0;
                try
                {
                    Logger.Info($"Executing GENERATE_FREQUENCY_SUSPECTS for TrxId > {PreviousTrxId}");

                    using (SqlConnection connection = new SqlConnection(_DefaultConnections))
                    {
                        SqlCommand sqlCommand = new SqlCommand("GENERATE_FREQUENCY_SUSPECTS", connection);
                        sqlCommand.CommandType = CommandType.StoredProcedure;
                        sqlCommand.CommandTimeout = 0;
                        sqlCommand.Parameters.Add("@PREVIOUS_TRXLOGID_INT", SqlDbType.VarChar).Value = (object)PreviousTrxId;
                        sqlCommand.Parameters.Add("@RECORD_COUNT", SqlDbType.Int);

                        if (ServiceRun.EnableRecheck == true)
                            sqlCommand.Parameters.Add("@RECHECK", SqlDbType.VarChar).Value = (object)RECHECK;

                        sqlCommand.Parameters["@RECORD_COUNT"].Direction = ParameterDirection.Output;
                        connection.Open();
                        sqlCommand.ExecuteNonQuery();
                        count = Convert.ToInt32(sqlCommand.Parameters["@RECORD_COUNT"].Value);
                        connection.Close();
                    }

                    Logger.Info($"Total Suspects Added : {count}");
                    return count;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "GenerateFrequencySuspect");
                    throw;
                }
            }

            #region TRANSACTION RECHECKS
            public async Task<string> RecheckTransactions(List<Parameter> parameters, List<DataRow> FetchTransactions)
            {
                try
                {
                    string concatedparameters = GetSelectParams(parameters);

                    await GetChangeofResponseCode(concatedparameters);

                    if (FetchTransactions.Count > 0 && FetchTransactions.Where(row => row["RESP_CODE"]?.ToString() == "056").Any())
                    {
                        DataTable NewRechecks = FetchTransactions.Where(row => row["RESP_CODE"]?.ToString() == "056").CopyToDataTable<DataRow>();

                        await InsertRecheckData(NewRechecks);
                    }

                    return _recheckTrxLogidCSV;

                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "RecheckTransactions");
                    throw;
                }
            }
            public async Task<DataTable> GetChangeofResponseCode(string selectParams)
            {
                Logger.Info($"{DateTime.Now}: Pulling Recheck Transactions From Recheck_OnResponse Table");

                List<string> recheckTrxLogid = await GetOldRechecks();

                _recheckTrxLogidCSV = string.Join(",", recheckTrxLogid);

                DataTable changeofResponseCode = new DataTable();

                if (recheckTrxLogid.Count > 0)
                {
                    try
                    {
                        using (OracleConnection connection = new OracleConnection(_OracleConnection))
                        {
                            await connection.OpenAsync();

                            // Keep below Oracle's 1000-item IN limitation
                            const int batchSize = 900;

                            for (int i = 0; i < recheckTrxLogid.Count; i += batchSize)
                            {
                                var batch = recheckTrxLogid.Skip(i).Take(batchSize).ToList();

                                string query = $@"SELECT DISTINCT {selectParams} FROM {_Schema}
                                                WHERE TRAN_CODE IN ('00', '01', '44', '9017', '30', '73', '9011') AND 
                                                RESP_CODE IN('000', '001', '004', '014', '024', '025', '060', '094', '997')
                                                AND TRANSACTION_LOG_ID IN :ids
                                                ORDER BY TRANSACTION_LOG_ID ASC";

                                using (var command = connection.CreateCommand())
                                {
                                    command.CommandText = query;
                                    command.CommandTimeout = 300;

                                    for (int j = 0; j < batch.Count; j++)
                                    {
                                        command.Parameters.Add(
                                            $"id{j}",
                                            Oracle.ManagedDataAccess.Client.OracleDbType.Int64
                                        ).Value = batch[j];
                                    }

                                    using (var adapter = new Oracle.ManagedDataAccess.Client.OracleDataAdapter(command))
                                    {
                                        adapter.Fill(changeofResponseCode);
                                    }
                                }

                                Logger.Info($"GetChangeofResponseCode: Processed batch " + $"{(i / batchSize) + 1}, IDs: {batch.Count}");
                            }
                            if (changeofResponseCode.Rows.Count > 0)
                                await UpdateRecheckedTransactions(changeofResponseCode);
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "GetChangeofResponseCode");
                        throw;
                    }
                }
                Logger.Info($"{DateTime.Now}: Finish Pulling Recheck Transactions From Recheck_OnResponse Table");
                return changeofResponseCode;
            }
            public async Task<List<string>> GetOldRechecks()
            {
                List<string> recheckTrxLogids = new List<string>();
                DataTable dataTable = new DataTable();

                Logger.Info($"{DateTime.Now}: Calling GetRecheckTrx_LOGID");

                try
                {
                    using (SqlConnection connection = new SqlConnection(_DefaultConnections))
                    {
                        await connection.OpenAsync();

                        using (SqlCommand selectCommand = new SqlCommand("SELECT DISTINCT TRXLOGID FROM Recheck_OnResponse", connection))
                        {
                            selectCommand.CommandTimeout = 30;

                            using (SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(selectCommand))
                            {
                                sqlDataAdapter.Fill(dataTable);
                            }
                        }
                    }

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row["TRXLOGID"] != DBNull.Value)
                        {
                            recheckTrxLogids.Add(row["TRXLOGID"].ToString());
                        }
                    }

                    if (recheckTrxLogids.Count > 0)
                    {
                        Logger.Info($"{DateTime.Now}: GetRecheckTrx_LOGID Returned: {recheckTrxLogids.Count}");
                    }
                    else
                    {
                        Logger.Info($"{DateTime.Now}: No RecheckTRX_LOGID Returned");
                    }

                    return recheckTrxLogids;
                }
                catch (Exception ex)
                {
                    Logger.Error($"{DateTime.Now}: Error in GetRecheckTrx_LOGID: {ex}");
                    throw;
                }
            }

            public async Task UpdateRecheckedTransactions(DataTable data)
            {
                Logger.Info($"{DateTime.Now}:Updating ISS Transactions with Recheck Cases");

                try
                {
                    using (SqlConnection connection = new SqlConnection(_DefaultConnections))
                    {
                        await connection.OpenAsync();

                        // 1. Create temporary staging table
                        string createTempTable = @"
                    CREATE TABLE #RecheckUpdates
                    (
                        TRANSACTION_LOG_ID BIGINT NOT NULL,
                        RESP_CODE VARCHAR(50) NULL
                    );";

                        using (SqlCommand command = new SqlCommand(createTempTable, connection))
                        {
                            command.CommandTimeout = 0;
                            await command.ExecuteNonQueryAsync();
                        }

                        // 2. Prepare DataTable
                        DataTable table = new DataTable();

                        table.Columns.Add("TRANSACTION_LOG_ID", typeof(long));
                        table.Columns.Add("RESP_CODE", typeof(string));

                        foreach (DataRow row in data.AsEnumerable().ToList())
                        {
                            DataRow dr = table.NewRow();

                            dr["TRANSACTION_LOG_ID"] = row["TRANSACTION_LOG_ID"];

                            dr["RESP_CODE"] = row["RESP_CODE"] == null ? DBNull.Value : row["RESP_CODE"];

                            table.Rows.Add(dr);
                        }

                        // 3. Bulk insert into temporary table
                        using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
                        {
                            bulkCopy.DestinationTableName = "#RecheckUpdates";
                            bulkCopy.BatchSize = 10000;
                            bulkCopy.BulkCopyTimeout = 0;

                            bulkCopy.ColumnMappings.Add("TRANSACTION_LOG_ID", "TRANSACTION_LOG_ID");
                            bulkCopy.ColumnMappings.Add("RESP_CODE", "RESP_CODE");

                            await bulkCopy.WriteToServerAsync(table);
                        }

                        // 4. Update PaySys in ONE operation
                        string updateQuery = $@"
                            UPDATE p
                            SET p.RESP_CODE = r.RESP_CODE
                            FROM {_LocalSchema} p
                            INNER JOIN #RecheckUpdates r
                            ON p.TRANSACTION_LOG_ID = r.TRANSACTION_LOG_ID;

                            UPDATE p
                            SET p.RESP_CODE = r.RESP_CODE
                            FROM {_LocalHistorySchema} p
                            INNER JOIN #RecheckUpdates r
                            ON p.TRANSACTION_LOG_ID = r.TRANSACTION_LOG_ID
                            WHERE p.TIME_LOC_TRAN_DT >= DATEADD(HOUR,-2,getdate());";

                        using (SqlCommand command = new SqlCommand(updateQuery, connection))
                        {
                            command.CommandTimeout = 0;

                            int updated = await command.ExecuteNonQueryAsync();

                            Logger.Info(
                                $"{DateTime.Now}: ISS records updated: {updated}");
                        }

                        // 5. Delete recheck records in ONE operation
                        string deleteQuery = @"
                            DELETE r
                            FROM Recheck_OnResponse r
                            INNER JOIN #RecheckUpdates u
                                ON r.TRXLOGID = u.TRANSACTION_LOG_ID;";

                        using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                        {
                            command.CommandTimeout = 0;

                            int deleted = await command.ExecuteNonQueryAsync();

                            Logger.Info(
                                $"{DateTime.Now}: Recheck records deleted: {deleted}");
                        }
                    }

                    Logger.Info($"{DateTime.Now}: Updation Completed");
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "UpdateRecheckedTransactions");
                    throw;
                }
            }



            public async Task InsertRecheckData(DataTable ToRecheckTransactrions)
            {
                Logger.Info($"{DateTime.Now} : Calling InsertRecheckData");

                DataTable dataTable = new DataTable();

                dataTable.Columns.Add("TRXLOGID", typeof(long));
                dataTable.Columns.Add("TRX_DATETIME", typeof(DateTime));
                dataTable.Columns.Add("RESP_CODE", typeof(string));

                foreach (DataRow transaction in ToRecheckTransactrions.Rows)
                {
                    DataRow row = dataTable.NewRow();

                    row["TRXLOGID"] = transaction["TRANSACTION_LOG_ID"];
                    row["TRX_DATETIME"] = transaction["time_loc_tran_dt"];
                    row["RESP_CODE"] = transaction["RESP_CODE"] ?? "";

                    dataTable.Rows.Add(row);
                }

                using (SqlConnection connection = new SqlConnection(_DefaultConnections))
                {
                    await connection.OpenAsync();

                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
                    {
                        bulkCopy.DestinationTableName = "Recheck_OnResponsePaysys";
                        bulkCopy.BatchSize = 5000;
                        bulkCopy.BulkCopyTimeout = 0;

                        bulkCopy.ColumnMappings.Add("TRXLOGID", "TRXLOGID");
                        bulkCopy.ColumnMappings.Add("TRX_DATETIME", "TRX_DATETIME");
                        bulkCopy.ColumnMappings.Add("RESP_CODE", "RESP_CODE");

                        await bulkCopy.WriteToServerAsync(dataTable);
                    }
                }

                Logger.Info($"{DateTime.Now}: InsertRecheckData: " + ToRecheckTransactrions.Rows.Count + " Rows Inserted");
            }





            public async Task RemoveOldRechecks()
            {
                Logger.Info(@$"{DateTime.Now}: Deleting Transaction From Recheck_OnResponse");
                try
                {
                    int num;
                    using (SqlConnection connection = new SqlConnection(_DefaultConnections))
                    {
                        string SqlQuery = $"DELETE FROM Recheck_OnResponse WHERE TRX_DATETIME < DATEADD(SECOND, -{_RecheckTimeout}, GETDATE())";
                        await connection.OpenAsync();
                        using (SqlCommand sqlCommand = new SqlCommand(SqlQuery, connection))
                        {
                            sqlCommand.CommandTimeout = 0;
                            num = await sqlCommand.ExecuteNonQueryAsync();
                        }
                    }
                    Logger.Info($"{DateTime.Now}: {num} Rows Deleted");
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "RemoveOldRechecks");
                    throw;
                }
            }

            #endregion
        }
    }
}
