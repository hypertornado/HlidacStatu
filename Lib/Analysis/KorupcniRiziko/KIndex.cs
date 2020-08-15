﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HlidacStatu.Util.Cache;

namespace HlidacStatu.Lib.Analysis.KorupcniRiziko
{
    public class KIndex
    {
        private static CouchbaseCacheManager<KIndexData, string> instanceByIco
       = CouchbaseCacheManager<KIndexData, string>.GetSafeInstance("kindexByICO", KIndexData.GetDirect,
#if (!DEBUG)
                TimeSpan.FromMinutes(120)
#else
                TimeSpan.FromSeconds(1)
#endif
           );

        public static KIndexData Get(string ico)
        {
            if (string.IsNullOrEmpty(ico))
                return null;
            var f = instanceByIco.Get(ico);
            return f;
        }

    }
}
