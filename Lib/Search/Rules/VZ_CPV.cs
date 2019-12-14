﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HlidacStatu.Lib.Search.Rules
{
    public class VZ_CPV
        : RuleBase
    {
        public VZ_CPV(bool stopFurtherProcessing = false, string addLastCondition = "")
            : base("", stopFurtherProcessing, addLastCondition)
        { }


        protected override RuleResult processQueryPart(SplittingQuery.Part part)
        {
            if (part == null)
                return null;

            if (part.Prefix == "cpv:")
            {

                string cpv = "";
                //Match m = Regex.Match(modifiedQ, lookFor, regexQueryOption);
                //string cpv = "";
                //if (m.Success)
                //    cpv = m.Groups["q"].Value;
                cpv = part.Value;
                if (!string.IsNullOrEmpty(cpv))
                {
                    string[] cpvs = cpv.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                    string q_cpv = "";
                    if (cpvs.Length > 0)
                        q_cpv = " ( " + cpvs.Select(s => "cpv:" + s + "*").Aggregate((f, s) => f + " OR " + s) + " ) ";

                    return new RuleResult(SplittingQuery.SplitQuery("{q_cpv}"), this.NextStep);
                }
            }


            return null;// new RuleResult(part, this.NextStep);
        }

    }
}
