﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxDrill.DaxHelpers
{
    public class DaxFilter
    {
        public string TableName;
        public string ColumnName; // also represents hierarchy name
        public string Value;
        public IList<DaxColumn> ColumnNameHierarchy;
        public string[] ValueHierarchy;

        public bool IsHierarchy;

        // also represents the table-qualified column name
        public string Key
        {
            get
            {
                return "[" + this.TableName + "].[" + this.ColumnName + "]";
            }
        }

        // TODO: use HierarchyValue instead of Value which is limited to the 1st value
        public string MDX
        {
            get
            {
                return Key + ".&[" + this.Value + "]";
            }
        }

        public string GetValue(string columnName)
        {
            var daxFilter = this;
            var matchedColumn = daxFilter.ColumnNameHierarchy.Where(x => x.ColumnName == columnName).FirstOrDefault();
            return daxFilter.ValueHierarchy[matchedColumn.Index];
        }

    }
}
