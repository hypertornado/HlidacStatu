﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HlidacStatu.Lib.Search.Rules
{
    public class VZ_Form
        : RuleBase
    {
        public VZ_Form( bool stopFurtherProcessing = false, string addLastCondition = "")
            : base("", stopFurtherProcessing, addLastCondition)
        { }


        protected override RuleResult processQueryPart(SplittingQuery.Part part)
        {
            if (part == null)
                return null;

            if (part.Prefix == "form:")
            {
                string form = part.Value;

                if (!string.IsNullOrEmpty(form))
                {
                    string[] forms = form.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                    string q_form = "";
                    if (forms.Length > 0)
                        q_form = "formulare.druh:(" + forms.Select(s => s + "*").Aggregate((f, s) => f + " OR " + s) + ")";

                    return new RuleResult(SplittingQuery.SplitQuery($" ( {q_form} ) "), this.NextStep);
                }
            }


            return null;
        }

    }
}
