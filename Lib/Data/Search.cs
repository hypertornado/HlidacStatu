﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HlidacStatu.Lib.Data
{
    public partial class Search
    {
        public static int Limit = 50;
        public static int FirmyLimit = 1000;


        public class GeneralResult<T> : ISearchResult
        {
            public string Query { get; set; }
            public IEnumerable<T> Result { get; private set; }
            public long Total { get; private set; }
            public int PageSize { get; private set; }
            public int Page { get; set; }
            public string Order { get; set; }
            public bool IsValid { get; private set; }
            public bool HasResult { get { return IsValid && Total > 0; } }
            public string DataSource { get; set; }
            public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;

            public virtual int MaxResultWindow() { return Lib.Searching.Tools.MaxResultWindow; }

            public GeneralResult(string query, IEnumerable<T> result, int pageSize)
                : this(query, result?.Count() ?? 0, result, pageSize, true)
            { }

            public GeneralResult(string query, long total, IEnumerable<T> result, int pageSize, bool isValid = true)
            {
                this.Query = query;
                this.Result = result;
                this.Total = total;
                this.IsValid = isValid;
                this.PageSize = PageSize;
            }


            public object ToRouteValues(int page)
            {
                return new
                {
                    Q = this.Query,
                    Page = page
                };
            }

        }


    }
}
