﻿extern alias AnalysisServices2014;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using ADOMD = Microsoft.AnalysisServices.AdomdClient;
using DaxDrill.DaxHelpers;
using Microsoft.AnalysisServices.Tabular;
using DaxDrill.ExcelHelpers;
using DaxDrill.Tabular;
using SSAS12 = AnalysisServices2014::Microsoft.AnalysisServices;

namespace DaxDrill.Tests
{
    [TestFixture]
    public class ProductionTests
    {
        /*
EVALUATE
CALCULATETABLE (
TOPN( 100, Usage ),
Usage[Inbound or Outbound] = "Inbound",
Usage[Call Type] = "MOC",
UsageDate[Usage_Year] = "2014",
UsageDate[Usage_MonthAbbrev] = "May" 
)
         */

        public const string serverName = "FINSERV01";

         [Test]
        public void ParseCellDictionary()
        {
            #region Arrange

            var excelDic = new Dictionary<string, string>();
            excelDic.Add("[Usage].[Inbound or Outbound].[Inbound or Outbound]",
                "[Usage].[Inbound or Outbound].&[Inbound]");
            excelDic.Add("[Usage].[Call Type].[Call Type]",
                "[Usage].[Call Type].&[MOC]");
            excelDic.Add("[UsageDate].[Usage_Year].[Usage_Year]",
                "[UsageDate].[Usage_Year].&[2014]");
            excelDic.Add("[UsageDate].[Usage_MonthAbbrev].[Usage_MonthAbbrev]",
                "[UsageDate].[Usage_MonthAbbrev].&[May]");
            #endregion

            #region Parse
            var pivotCellDic = new PivotCellDictionary();
            pivotCellDic.SingleSelectDictionary = excelDic;
            string commandText;
            using (var tabular = new DaxDrill.Tabular.TabularHelper(serverName, "Roaming"))
            {
                tabular.Connect();
                commandText = DaxDrillParser.BuildFilterCommandText(pivotCellDic, tabular, null);
                tabular.Disconnect();
            }
            #endregion

            #region Assert
            Console.WriteLine(commandText);

            #endregion
        }

        [Test]
        public void ParseAll()
        {
            #region Arrange

            var singDic = new Dictionary<string, string>();
            singDic.Add("[Usage].[Inbound or Outbound].[Inbound or Outbound]",
                "[Usage].[Inbound or Outbound].&[Inbound]");
            singDic.Add("[Usage].[Call Type].[Call Type]",
                "[Usage].[Call Type].&[MOC]");
            singDic.Add("[UsageDate].[Usage_Year].[Usage_Year]",
                "[UsageDate].[Usage_Year].&[2014]");
            singDic.Add("[UsageDate].[Usage_MonthAbbrev].[Usage_MonthAbbrev]",
                "[UsageDate].[Usage_MonthAbbrev].&[May]");
            #endregion

            #region Parse
            string commandText;

            var pivotCellDic = new PivotCellDictionary();
            pivotCellDic.SingleSelectDictionary = singDic;

            using (var tabular = new DaxDrill.Tabular.TabularHelper(serverName, "Roaming"))
            {
                tabular.Connect();
                commandText = DaxDrillParser.BuildQueryText(tabular, pivotCellDic, "Gross Billed Sum", 99999);
                tabular.Disconnect();
            }
            #endregion

            #region Assert
            Console.WriteLine(commandText);

            #endregion
        }

        [Test]
        public void GetMeasure()
        {
            string measureName = "Gross Billed Sum";
            TabularItems.Measure measure = null;
            using (var tabular = new DaxDrill.Tabular.TabularHelper(serverName, "Roaming"))
            {
                tabular.Connect();
                measure = tabular.GetMeasure(measureName);
                tabular.Disconnect();
            }
            Console.WriteLine("Measure = {0}, Table = {1}", measure.Name, measure.TableName);
        }

        [Test]
        public void ParseSelectedColumns()
        {
            #region Arrange

            var singDic = new Dictionary<string, string>();
            singDic.Add("[Usage].[Inbound or Outbound].[Inbound or Outbound]",
                "[Usage].[Inbound or Outbound].&[Inbound]");
            singDic.Add("[Usage].[Call Type].[Call Type]",
                "[Usage].[Call Type].&[MOC]");
            singDic.Add("[UsageDate].[Usage_Year].[Usage_Year]",
                "[UsageDate].[Usage_Year].&[2014]");
            singDic.Add("[UsageDate].[Usage_MonthAbbrev].[Usage_MonthAbbrev]",
                "[UsageDate].[Usage_MonthAbbrev].&[May]");
            #endregion

            #region Parse
            var pivotCellDic = new PivotCellDictionary();
            pivotCellDic.SingleSelectDictionary = singDic;

            var parser = new DaxDrillParser();
            string commandText;
            using (var tabular = new DaxDrill.Tabular.TabularHelper(serverName, "Roaming"))
            {
                tabular.Connect();
                var selectedColumns = new List<DetailColumn>();
                selectedColumns.Add(new DetailColumn() { Name = "Call Type", Expression = "Usage[Call Type]" });
                selectedColumns.Add(new DetailColumn() { Name = "Call Type Description", Expression = "Usage[Call Type Description]" });
                commandText = DaxDrillParser.BuildQueryText(tabular, pivotCellDic, "Gross Billed Sum", 99999, selectedColumns, null);
                tabular.Disconnect();
            }
            #endregion

            #region Assert
            Console.WriteLine(commandText);

            #endregion
        }

        [Test]
        public void GetMeasureDMV()
        {
            var tabular = new DaxDrill.Tabular.TabularHelper_2016(serverName, "CashFlow");
            tabular.Connect();
            var measure = tabular.GetMeasureFromDMV("Func Amt Sum");

            Console.WriteLine(measure.TableName);
            Assert.AreEqual("CashFlow", measure.TableName);
        }

        public void GetColumnDataType_SSAS2014()
        {
            var tabular = new DaxDrill.Tabular.TabularHelper_2014(serverName, "ApPayments");
            tabular.Connect();


            var table = tabular.GetTable("Account");
            foreach (SSAS12.CubeAttribute attr in table.BaseTable12.Attributes)
            {
                //if (attr.Attribute.Name == "Is Cash")
                Console.WriteLine("{0} | {1}", attr.Attribute.Name, attr.Attribute.KeyColumns[0].DataType.ToString());
            }

        }

        public void GetMeasure_SSAS2016()
        {
            var tabular = new DaxDrill.Tabular.TabularHelper("FINSERV01", "HRR_Snap");
            tabular.Connect();
            TabularItems.Measure measure = tabular.GetMeasure("HRR_ExGST_Sum");
            Console.WriteLine(measure.Name);
        }

        public void GetMeasure_SSAS2014()
        {
            var tabular = new DaxDrill.Tabular.TabularHelper_2014("FINSERV01", "CashFlow");
            tabular.Connect();
            var measure = tabular.GetMeasure("Func Amt Sum");

            Console.WriteLine(measure.Name);
        }

        public void GetTable_SSAS2016()
        {
            var commandText = "EVALUATE TOPN( 100, HRR_Snap )";
            string serverName = "FINSERV01";
            string connectionString = string.Format(
                "Integrated Security=SSPI;Persist Security Info=True;Initial Catalog={1};Data Source={0};", serverName, "HRR_Snap");

            var cnn = new ADOMD.AdomdConnection(connectionString);
            var daxClient = new DaxClient();
            var dtResult = daxClient.ExecuteTable(commandText, cnn);

            var columnNames = "";
            foreach (System.Data.DataColumn column in dtResult.Columns)
            {
                if (string.IsNullOrEmpty(columnNames))
                    columnNames += ",";
                columnNames += column.ColumnName;

            }
            Console.WriteLine(columnNames);

            foreach (System.Data.DataRow row in dtResult.Rows)
            {
                var rowText = "";
                foreach (object item in row.ItemArray)
                {
                    if (!string.IsNullOrEmpty(rowText))
                        rowText += ",";
                    rowText += Convert.ToString(item);
                }
                Console.WriteLine(rowText);
            }
        }

        public void GetTable_SSAS2014()
        {
            var commandText = "EVALUATE TOPN( 100, CashFlow )";
            string serverName = "FINSERV01";
            string connectionString = string.Format(
                "Integrated Security=SSPI;Persist Security Info=True;Initial Catalog={1};Data Source={0};", serverName, "CashFlow");

            var cnn = new ADOMD.AdomdConnection(connectionString);
            var daxClient = new DaxClient();
            var dtResult = daxClient.ExecuteTable(commandText, cnn);

            var columnNames = "";
            foreach (System.Data.DataColumn column in dtResult.Columns)
            {
                if (string.IsNullOrEmpty(columnNames))
                    columnNames += ",";
                columnNames += column.ColumnName;
                
            }
            Console.WriteLine(columnNames);

            foreach (System.Data.DataRow row in dtResult.Rows)
            {
                var rowText = "";
                foreach (object item in row.ItemArray)
                {
                    if (!string.IsNullOrEmpty(rowText))
                        rowText += ",";
                    rowText += Convert.ToString(item);
                }
                Console.WriteLine(rowText);
            }
        }
    }
}
