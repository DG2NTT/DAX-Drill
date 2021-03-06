﻿using DaxDrill.DaxHelpers;
using DaxDrill.ExcelHelpers;
using Microsoft.AnalysisServices.Tabular;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Excel = Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;

namespace DaxDrill.Logic
{
    public class QueryClient
    {
        private readonly Excel.Range rngCell;
        private readonly Excel.PivotTable pivotTable;
        private readonly string connectionString;
        private readonly Excel.PivotCache pcache;
        private readonly PivotCellDictionary pivotCellDic;
        private readonly IEnumerable<string> pivotFieldNames;

        public QueryClient(Excel.Range rngCell) : 
            this(rngCell, null, null)
        {
        }

        public QueryClient(Excel.Range rngCell, PivotCellDictionary pivotCellDic, IEnumerable<string> pivotFieldNames)
        {
            this.rngCell = rngCell;
            this.pivotTable = rngCell.PivotTable;
            this.pcache = pivotTable.PivotCache();
            this.connectionString = pcache.Connection;
            this.pivotFieldNames = pivotFieldNames ?? PivotCellHelper.GetPivotFieldNames(rngCell);
            this.pivotCellDic = pivotCellDic ?? PivotCellHelper.GetPivotCellQuery(rngCell);
        }

        public Excel.Range RangeCell
        {
            get { return rngCell; }
        }

        public string GetDAXQuery()
        {
            return GetDAXQuery(connectionString);
        }

        public string GetDAXQuery(string connString)
        {
            string commandText = "";
   
            var cnnStringBuilder = new TabularConnectionStringBuilder(connString);

            int maxRecords = ExcelHelper.GetMaxDrillthroughRecords(rngCell);
            var detailColumns = GetCustomDetailColumns(rngCell);

            using (var tabular = new DaxDrill.Tabular.TabularHelper(
                cnnStringBuilder.DataSource,
                cnnStringBuilder.InitialCatalog))
            {
                tabular.Connect();

                // use Table Query if it exists
                // otherwise get the Table Name from the Measure

                string tableQuery = GetCustomTableQuery(rngCell);

                if (string.IsNullOrEmpty(tableQuery))
                {
                    // if table not defined in XML metadata, retrieve entire table
                    string measureName = GetMeasureName(rngCell);
                    commandText = DaxDrillParser.BuildQueryText(tabular,
                        pivotCellDic,
                        measureName, maxRecords, detailColumns, pivotFieldNames);
                }
                else
                {
                    // if table is defined in XML metadata, retrieve using DAX command
                    commandText = DaxDrillParser.BuildCustomQueryText(tabular,
                        pivotCellDic,
                        tableQuery, maxRecords, detailColumns, pivotFieldNames);
                }

                tabular.Disconnect();
            }

            return commandText;
        }

        public bool IsDatabaseCompatible(string connString)
        {
            var cnnStringBuilder = new TabularConnectionStringBuilder(connString);
            bool result = false;

            using (var tabular = new DaxDrill.Tabular.TabularHelper(
                cnnStringBuilder.DataSource,
                cnnStringBuilder.InitialCatalog))
            {
                tabular.Connect();
                result = tabular.IsDatabaseCompatible;
                tabular.Disconnect();
            }
            return result;
        }

        public IEnumerable<DetailColumn> GetCustomDetailColumns(Excel.Range rngCell)
        {
            Excel.WorkbookConnection wbcnn = null;
            Excel.Workbook workbook = null;
            Excel.Worksheet sheet = null;

            wbcnn = ExcelHelper.GetWorkbookConnection(rngCell);

            sheet = (Excel.Worksheet)rngCell.Parent;
            workbook = (Excel.Workbook)sheet.Parent;

            TabularItems.Measure measure = GetMeasure(rngCell);

            string xmlString = ExcelHelper.ReadCustomXmlNode(
                workbook, Constants.DaxDrillXmlSchemaSpace,
                string.Format("{0}[@id='{1}']", Constants.TableXpath, measure.TableName));
            List<DetailColumn> columns = DaxDrillConfig.GetColumnsFromTableXml(Constants.DaxDrillXmlSchemaSpace, xmlString, wbcnn.Name, measure.TableName);

            return columns;
        }

        // get DAX query from XML Data based on active rngCell
        public string GetCustomTableQuery(Excel.Range rngCell)
        {
            Excel.Worksheet sheet = (Excel.Worksheet)rngCell.Parent;
            Excel.Workbook workbook = (Excel.Workbook)sheet.Parent;

            TabularItems.Measure measure = GetMeasure(rngCell);

            #region measure
            // get referenced measure
            Office.CustomXMLNode node = ExcelHelper.GetCustomXmlNode(workbook, Constants.DaxDrillXmlSchemaSpace,
                string.Format("{0}[@id='{1}']", Constants.MeasureXpath, measure.Name));

            string measureName = measure.Name;

            if (node != null)
            {
                foreach (Office.CustomXMLNode attr in node.Attributes)
                {
                    if (attr.BaseName == "ref")
                    {
                        // get DAX query by measure id
                        node = ExcelHelper.GetCustomXmlNode(workbook, Constants.DaxDrillXmlSchemaSpace,
                            string.Format("{0}[@id='{1}']/x:query", Constants.MeasureXpath, attr.Text));
                        break;
                    }
                }
            }
                
            #endregion

            #region table

            // get DAX query by table id (if measure not found in XML metadata)
            if (node == null)
                node = ExcelHelper.GetCustomXmlNode(workbook, Constants.DaxDrillXmlSchemaSpace,
                    string.Format("{0}[@id='{1}']/x:query", Constants.TableXpath, measure.TableName));

            #endregion

            if (node != null)
                return node.Text;

            return string.Empty;
        }


        private TabularItems.Measure GetMeasure(Excel.Range rngCell)
        {
            var cnnBuilder = new TabularConnectionStringBuilder(this.connectionString);

            string measureName = GetMeasureName(rngCell);
            TabularItems.Measure measure = null;
            using (var tabular = new DaxDrill.Tabular.TabularHelper(cnnBuilder.DataSource, cnnBuilder.InitialCatalog))
            {
                tabular.Connect();
                measure = tabular.GetMeasure(measureName);
                tabular.Disconnect();
            }
            return measure;
        }

        public static string GetMeasureName(Excel.Range rngCell)
        {
            Excel.PivotItem pi = null;
            pi = rngCell.PivotItem;
            string piName = pi.Name;
            return DaxDrillParser.GetMeasureFromPivotItem(piName);
        }

        public static bool IsDrillThroughEnabled(Excel.Range rngCell)
        {
            try
            {
                Excel.PivotTable pt = rngCell.PivotTable; // throws error if selected cell is not pivot cell
                Excel.PivotCache cache = pt.PivotCache();
                Excel.Workbook workbook = rngCell.Worksheet.Parent;

                if (!cache.OLAP) return false;

                if (ExcelHelper.CountXmlNamespace(workbook, Constants.DaxDrillXmlSchemaSpace) == 0)
                    return false;

                // check compatibility of Tabular database
                var queryClient = new QueryClient(rngCell);
                var connString = ExcelHelper.GetConnectionString(rngCell);
                if (!queryClient.IsDatabaseCompatible(connString)) return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
