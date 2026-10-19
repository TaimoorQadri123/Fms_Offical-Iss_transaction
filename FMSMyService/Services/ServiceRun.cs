using FMSMyService;
using FMSMyService.Helpers;
using FMSMyService.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace FMSISSTransactionService.Services
{
    public class ServiceRun
    {
        private readonly CommonMethods _commonMethods;
        private readonly ILogger<ServiceRun> _logger;
        private readonly IConfiguration _config;
        
        public DateTime PreviousDateTime { get; set; }
        public long PreviousTrxLogId { get; set; }

        public List<Rules> IISRules { get; set; } = new List<Rules>();
        public IEnumerable<RulesAndCriterias> ISSCriterias { get; set; } = new List<RulesAndCriterias>();
        public List<Parameter> Parameters { get; set; } = new List<Parameter>();
        public static bool EnableRecheck { get; set; } = false;
        public static bool _EnableDBAlteration { get; set; } = true;
        public ServiceRun(CommonMethods commonMethods, ILogger<ServiceRun> logger, IConfiguration config)
        {
            _commonMethods = commonMethods;
            _logger = logger;
            _config = config;
            _EnableDBAlteration = _config.GetValue<bool>("ServiceSettings:EnableDBAlteration");
        }

        public async Task StartServices()
        {
            try
            {
                string recheckTrxLogidCSV = string.Empty;

                if (!string.IsNullOrEmpty(_config["LogSettings:LogDirectory"]))
                {
                    Logger.LogDirectory = _config["LogSettings:LogDirectory"]!;
                }

                Logger.Simple($"================================ Cycle Started ==================================");

                List<DataRow> dtTransactions = new List<DataRow>();

                // GET RULES
                Logger.Info("Fetching Rules");

                IISRules = _commonMethods.GetRules().ToList();

                if (IISRules.Any())
                    Logger.Info($"Rules Fetched : {IISRules.Count}");
                else
                    Logger.Warning("No Rules Found.");

                // GET PARAMETERS REMOVE WHEN VALIDATOR
                Parameters = _commonMethods.Get_Parameters().ToList();
                Logger.Info($"Parameters Fetched : {Parameters.Count}");

                // GET MODIFY POINTS
                Logger.Info("Fetching Modify Points...");
                ModifyPoints modifypoint = _commonMethods.GetModifyPoint();

                if (modifypoint != null)
                {
                    this.PreviousDateTime = modifypoint.ModifiedOn;
                    this.PreviousTrxLogId = modifypoint.TrxLogId2;
                    Logger.Info($"Previous Modify Point Found | DateTime : {this.PreviousDateTime:yyyy-MM-dd HH:mm:ss} | TrxLogId2 : {this.PreviousTrxLogId}");
                }
               

                if (Worker._IsExceptionOccurred)
                {
                    Logger.Warning($"Previous cycle had an exception OR Service Restarted. Deleting future transactions for TrxLogId >= {PreviousTrxLogId}");
                    _commonMethods.DeleteFutureTransactions(PreviousTrxLogId);
                }

                if(EnableRecheck == true && _EnableDBAlteration)
                    await _commonMethods.RemoveOldRechecks();

                if (modifypoint != null && !modifypoint.IsSeeded)
                {
                    Logger.Info("System Not Seeded. Loading Transactions...");
                    dtTransactions = await _commonMethods.GetDataSetLatestTrans(PreviousTrxLogId, Parameters);
                    Logger.Info($"Transactions Fetched : {dtTransactions.Count}");
                }
                else if(modifypoint != null)
                {
                    Logger.Info($"Loading Latest ISS Transactions after {PreviousTrxLogId}.");
                    dtTransactions = await _commonMethods.GetDataSetLatestTrans(PreviousTrxLogId, Parameters);
                    Logger.Info($"Latest Transactions Fetched : {dtTransactions.Count}");
                }
                if (EnableRecheck == true)
                    recheckTrxLogidCSV = await _commonMethods.RecheckTransactions(Parameters, dtTransactions);

                if (dtTransactions.Any() && _EnableDBAlteration == true)
                {
                    if (IISRules.Any())
                    {
                        Logger.Info("Transaction Processing Started...");
                        int counterFreq = _commonMethods.GenerateFrequencySuspect(PreviousTrxLogId, recheckTrxLogidCSV);
                        Logger.Info($"Total Transactions Processed : {dtTransactions.Count}");
                        Logger.Info($"Total Suspects Added : {counterFreq}");
                    }
                    else
                    {
                        Logger.Warning("No Active ISS Rules Found For Processing.");
                    }

                    DataRow lastTx = dtTransactions.MaxBy(x => Convert.ToInt64(x["TRANSACTION_LOG_ID"]));

                    if (lastTx != null)
                    {
                        long maxTrxLogId = Convert.ToInt64(lastTx["TRANSACTION_LOG_ID"]);
                        DateTime lasttrxdate = Convert.ToDateTime(lastTx["TIME_LOC_TRAN_DT"]);
                        await _commonMethods.UpdateModifyPointsAsync("ISS", lasttrxdate, maxTrxLogId);
                        Logger.Info($"Modify Points Updated Successfully | DateTime : {lasttrxdate:yyyy-MM-dd HH:mm:ss} | TrxLogId : {maxTrxLogId}");
                    }
                }
                else
                {
                    Logger.Warning("No New Transactions Found.");
                }

                Logger.Simple($"=============== Cycle Completed At : {DateTime.Now} ===========================");
                Logger.Simple("");

                Worker._IsExceptionOccurred = false;
            }
            catch (Exception ex)
            {
                Worker._IsExceptionOccurred = true;
                Logger.LogException(ex, "StartServices");
                _logger.LogError(ex, "Error occurred during ServiceRun execution.");
            }
        }
    }
}
