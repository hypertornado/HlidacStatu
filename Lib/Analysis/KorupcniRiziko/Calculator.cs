﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using HlidacStatu.Lib.Data;
using HlidacStatu.Util;

using Nest;

namespace HlidacStatu.Lib.Analysis.KorupcniRiziko
{

    public partial class Calculator
    {
        public static int[] CalculationYears = Enumerable.Range(2017, DateTime.Now.Year - 2017).ToArray();
        const int minPocetSmluvKoncentraceDodavatelu = 1;

        private Firma urad = null;
        Dictionary<int, Lib.Analysis.BasicData> _calc_SeZasadnimNedostatkem = null;
        Dictionary<int, Lib.Analysis.BasicData> _calc_UzavrenoOVikendu = null;
        Dictionary<int, Lib.Analysis.BasicData> _calc_ULimitu = null;
        Dictionary<int, Lib.Analysis.BasicData> _calc_NovaFirmaDodavatel = null;

        public string Ico { get; private set; }

        private KIndexData kindex = null;

        public Calculator(string ico)
        {
            this.Ico = ico;

            this.urad = Firmy.Get(this.Ico);
            if (urad.Valid == false)
                throw new ArgumentOutOfRangeException("invalid ICO");

            kindex = KIndexData.Get(ico);
        }

        object lockCalc = new object();
        public KIndexData GetData(bool refresh = false)
        {
            if (refresh)
                kindex = null;
            lock (lockCalc)
            {
                if (kindex == null)
                {
                    kindex = CalculateSourceData();
                }
            }

            return kindex;
        }

        private KIndexData CalculateSourceData()
        {
            this.InitData();
            foreach (var year in CalculationYears)
            {
                KIndexData.Annual data_rok = CalculateForYear(year);
                kindex.roky.Add(data_rok);
            }
            return kindex;
        }

        private void InitData()
        {


            kindex = new KIndexData();
            kindex.Ico = urad.ICO;
            kindex.UcetniJednotka = KIndexData.UcetniJednotkaInfo.Load(urad.ICO);


            _calc_SeZasadnimNedostatkem = AdvancedQuery.PerYear($"ico:{this.Ico} and chyby:zasadni");

            _calc_UzavrenoOVikendu = AdvancedQuery.PerYear($"ico:{this.Ico} AND (hint.denUzavreni:>0)");

            _calc_ULimitu = AdvancedQuery.PerYear($"ico:{this.Ico} AND ( hint.smlouvaULimitu:>0 )");


            _calc_NovaFirmaDodavatel = AdvancedQuery.PerYear($"ico:{this.Ico} AND ( hint.pocetDniOdZalozeniFirmy:>-50 AND hint.pocetDniOdZalozeniFirmy:<30 )");
        }

        private KIndexData.Annual CalculateForYear(int year)
        {
            decimal smlouvyZaRok = (decimal)urad.Statistic().BasicStatPerYear[year].Pocet;

            KIndexData.Annual ret = new KIndexData.Annual(year);
            var fc = new FinanceDataCalculator(this.Ico, year);
            ret.FinancniUdaje = fc.GetData();


            ret.PercSeZasadnimNedostatkem = smlouvyZaRok == 0 ? 0m : (decimal)_calc_SeZasadnimNedostatkem[year].Pocet / smlouvyZaRok;
            ret.PercSmlouvySPolitickyAngazovanouFirmou = this.urad.Statistic().RatingPerYear[year].PercentSPolitiky;

            ret.PercNovaFirmaDodavatel = smlouvyZaRok == 0 ? 0m : (decimal)_calc_NovaFirmaDodavatel[year].Pocet / smlouvyZaRok;
            ret.PercSmluvUlimitu = smlouvyZaRok == 0 ? 0m : (decimal)_calc_ULimitu[year].Pocet / smlouvyZaRok;
            ret.PercUzavrenoOVikendu = smlouvyZaRok == 0 ? 0m : (decimal)_calc_UzavrenoOVikendu[year].Pocet / smlouvyZaRok;

            ret.Smlouvy = this.urad.Statistic().BasicStatPerYear[year];
            ret.Statistika = this.urad.Statistic().RatingPerYear[year];


            string queryPlatce = $"icoPlatce:{this.Ico} AND datumUzavreni:[{year}-01-01 TO {year + 1}-01-01}}";
            //string queryAll = $"ico:{this.Ico} AND datumUzavreni:[{year}-01-01 TO {year + 1}-01-01}}";

            if (smlouvyZaRok >= minPocetSmluvKoncentraceDodavatelu)
            {
                ret.CelkovaKoncentraceDodavatelu = KoncentraceDodavateluCalculator(queryPlatce);
                if (ret.CelkovaKoncentraceDodavatelu != null)
                    ret.KoncentraceDodavateluBezUvedeneCeny
                        = KoncentraceDodavateluCalculator(queryPlatce + " AND cena:0", ret.CelkovaKoncentraceDodavatelu.PrumernaHodnotaSmluv);

                if (ret.PercSmluvUlimitu > 0)
                    ret.KoncentraceDodavateluCenyULimitu
                        = KoncentraceDodavateluCalculator(queryPlatce + " AND ( hint.smlouvaULimitu:>0 )", ret.CelkovaKoncentraceDodavatelu?.PrumernaHodnotaSmluv ?? 0);

                Dictionary<int, string> obory = Lib.Data.Smlouva.SClassification
                    .AllTypes
                    .Where(m => m.MainType)
                    .OrderBy(m => m.Value)
                    .ToDictionary(k => k.Value, v => v.SearchShortcut);

                ret.KoncetraceDodavateluObory = new List<KoncentraceDodavateluObor>();

                Devmasters.Core.Batch.Manager.DoActionForAll<int>(obory.Keys,
                    (oborid) =>
                    {

                        queryPlatce = $"icoPlatce:{this.Ico} AND datumUzavreni:[{year}-01-01 TO {year + 1}-01-01}} AND oblast:{obory[oborid]}";
                        var k = KoncentraceDodavateluCalculator(queryPlatce);
                        KoncentraceDodavateluIndexy kbezCeny = null;
                        if (k != null)
                        {
                            if (k.PocetSmluvBezCeny > 0)
                            {
                                kbezCeny = KoncentraceDodavateluCalculator(queryPlatce + " AND cena:0", k.PrumernaHodnotaSmluv == 0 ? 1 : k.PrumernaHodnotaSmluv);
                            }
                            ret.KoncetraceDodavateluObory.Add(new KoncentraceDodavateluObor()
                            {
                                OborId = oborid,
                                OborName = obory[oborid],
                                Koncentrace = k,
                                KoncentraceBezUvedeneCeny = kbezCeny
                            });
                        };
                        return new Devmasters.Core.Batch.ActionOutputData();
                    }, null, null, true, maxDegreeOfParallelism: 10
                    );
            }

            ret.TotalAveragePercSmlouvyPod50k = TotalAveragePercSmlouvyPod50k(ret.Rok);

            ret.PercSmlouvyPod50k = AveragePercSmlouvyPod50k(this.Ico, ret.Rok, ret.Smlouvy.Pocet);
            ret.PercSmlouvyPod50kBonus = SmlouvyPod50kBonus(ret.PercSmlouvyPod50k, ret.TotalAveragePercSmlouvyPod50k);


            ret = FinalCalculationKIdx(ret);

            return ret;
        }

        public KIndexData.Annual FinalCalculationKIdx(KIndexData.Annual ret)
        {
            decimal smlouvyZaRok = (decimal)urad.Statistic().BasicStatPerYear[ret.Rok].Pocet;

            if (smlouvyZaRok < Consts.MinSmluvPerYear)
            {
                ret.KIndex = Consts.MinSmluvPerYearKIndexValue;
                ret.KIndexIssues = new string[] { $"K-Index nespočítán. Méně než {Consts.MinSmluvPerYear} smluv za rok." };
            }
            else
                ret.KIndex = CalculateKIndex(ret);

            ret.LastUpdated = DateTime.Now;

            return ret;
        }

        public decimal CalculateKIndex(KIndexData.Annual datayear)
        {

            //https://docs.google.com/spreadsheets/d/1FhaaXOszHORki7t5_YEACWFZUya5QtqUnNVPAvCArGQ/edit#gid=0
            KIndexData.VypocetDetail vypocet = new KIndexData.VypocetDetail();
            var vradky = new List<KIndexData.VypocetDetail.Radek>();

            decimal val =
            //r5
            datayear.Statistika.PercentBezCeny * 10m;   //=10   C > 1  F > 2,5
            vradky.Add(new KIndexData.VypocetDetail.Radek(
                KIndexData.KIndexParts.PercentBezCeny,
                datayear.Statistika.PercentBezCeny,
                10m)
            );

            //r11
            val += datayear.PercSeZasadnimNedostatkem * 10m;  //=20
            vradky.Add(new KIndexData.VypocetDetail.Radek(
                KIndexData.KIndexParts.PercSeZasadnimNedostatkem,
                datayear.PercSeZasadnimNedostatkem,
                10m)
            );


            //r13
            val += datayear.CelkovaKoncentraceDodavatelu?.Herfindahl_Hirschman_Modified * 20m ?? 0; //=40
            vradky.Add(new KIndexData.VypocetDetail.Radek(
                KIndexData.KIndexParts.CelkovaKoncentraceDodavatelu,
                datayear.CelkovaKoncentraceDodavatelu?.Herfindahl_Hirschman_Modified ?? 0,
                20m)
            );


            //r15
            val += datayear.KoncentraceDodavateluBezUvedeneCeny?.Herfindahl_Hirschman_Modified * 20m ?? 0; //60
            vradky.Add(new KIndexData.VypocetDetail.Radek(
                KIndexData.KIndexParts.KoncentraceDodavateluBezUvedeneCeny,
                datayear.KoncentraceDodavateluBezUvedeneCeny?.Herfindahl_Hirschman_Modified ?? 0,
                20m)
            );

            //r17
            val += datayear.PercSmluvUlimitu * 10m;  //70
            vradky.Add(new KIndexData.VypocetDetail.Radek(
                KIndexData.KIndexParts.PercSmluvUlimitu,
                datayear.PercSmluvUlimitu,
                10m)
            );

            //r18
            val += datayear.KoncentraceDodavateluCenyULimitu?.Herfindahl_Hirschman_Modified * 10m ?? 0; //80
            vradky.Add(
                new KIndexData.VypocetDetail.Radek(
                    KIndexData.KIndexParts.KoncentraceDodavateluCenyULimitu,
                    datayear.KoncentraceDodavateluCenyULimitu?.Herfindahl_Hirschman_Modified ?? 0,
                10m)
            );

            //r19
            val += datayear.PercNovaFirmaDodavatel * 2m; //82
            vradky.Add(
                new KIndexData.VypocetDetail.Radek(KIndexData.KIndexParts.PercNovaFirmaDodavatel, datayear.PercNovaFirmaDodavatel, 2m)
                );


            //r21
            val += datayear.PercUzavrenoOVikendu * 2m; //84
            vradky.Add(
                new KIndexData.VypocetDetail.Radek(KIndexData.KIndexParts.PercUzavrenoOVikendu, datayear.PercUzavrenoOVikendu, 2m)
            );


            //r22
            val += datayear.PercSmlouvySPolitickyAngazovanouFirmou * 2m; //86
            vradky.Add(new KIndexData.VypocetDetail.Radek(
                KIndexData.KIndexParts.PercSmlouvySPolitickyAngazovanouFirmou, datayear.PercSmlouvySPolitickyAngazovanouFirmou, 2m
                )
            );


            //oborova koncentrace
            var oboryKoncentrace = datayear.KoncetraceDodavateluObory
                .Where(m =>
                        m.Koncentrace.CelkovaHodnotaSmluv > (datayear.Smlouvy.CelkemCena * 0.05m)
                        || m.Koncentrace.PocetSmluv > (datayear.Smlouvy.Pocet * 0.05m)
                        || (m.Koncentrace.PocetSmluvBezCeny > datayear.Statistika.NumBezCeny * 0.02m)
                        )
                .ToArray(); //for debug;

            decimal prumernaCenaSmluv = datayear.Smlouvy.CelkemCena / (decimal)datayear.Smlouvy.Pocet;
            var oboryVahy = oboryKoncentrace
                .Select(m => new KIndexData.VypocetOboroveKoncentrace.RadekObor()
                {
                    Obor = m.OborName,
                    Hodnota = m.Combined_Herfindahl_Hirschman_Modified(),
                    Vaha = m.Koncentrace.CelkovaHodnotaSmluv + (prumernaCenaSmluv * (decimal)m.Koncentrace.PocetSmluv * m.PodilSmluvBezCeny),
                    PodilSmluvBezCeny = m.PodilSmluvBezCeny,
                    CelkovaHodnotaSmluv = m.Koncentrace.CelkovaHodnotaSmluv,
                    PocetSmluvCelkem = m.Koncentrace.PocetSmluv
                })
                .ToArray();
            decimal avg = oboryVahy.WeightedAverage(m => m.Hodnota, w => w.Vaha);
            val += avg * 14m; //100
            vradky.Add(
                new KIndexData.VypocetDetail.Radek(KIndexData.KIndexParts.KoncentraceDodavateluObory, avg, 14m)
                );
            vypocet.OboroveKoncentrace = new KIndexData.VypocetOboroveKoncentrace();
            vypocet.OboroveKoncentrace.PrumernaCenaSmluv = prumernaCenaSmluv;
            vypocet.OboroveKoncentrace.Radky = oboryVahy.ToArray();
            //
            //r16 - bonus!
            val -= datayear.PercSmlouvyPod50kBonus * 2m;

            vradky.Add(new KIndexData.VypocetDetail.Radek(
                KIndexData.KIndexParts.PercSmlouvyPod50kBonus, -1 * datayear.PercSmlouvyPod50kBonus, 2m
                )
            );
            vypocet.Radky = vradky.ToArray();

            if (val < 0)
                val = 0;

            var kontrolniVypocet = vypocet.Vypocet();
            if (val != kontrolniVypocet)
                throw new ApplicationException("Nesedi vypocet");

            vypocet.LastUpdated = DateTime.Now;
            datayear.KIndexVypocet = vypocet;


            return val;

        }



        public static decimal AveragePercSmlouvyPod50k(string ico, int year, long pocetSmluvCelkem)
        {
            if (pocetSmluvCelkem == 0)
                return 0;

            var res = HlidacStatu.Lib.Data.Smlouva.Search.SimpleSearch($"ico:{ico} cena:>0 AND cena:<=50000 AND datumUzavreni:[{year}-01-01 TO {year + 1}-01-01}}"
, 1, 0, HlidacStatu.Lib.Data.Smlouva.Search.OrderResult.FastestForScroll, null, exactNumOfResults: true);

            decimal perc = (decimal)(res.Total) / (decimal)pocetSmluvCelkem;
            return perc;
        }


        static Dictionary<int, decimal> totalsAvg50k = new Dictionary<int, decimal>();
        static object totalsAvg50kLock = new object();
        public static decimal TotalAveragePercSmlouvyPod50k(int year)
        {
            if (!totalsAvg50k.ContainsKey(year))
            {
                lock (totalsAvg50kLock)
                {
                    if (!totalsAvg50k.ContainsKey(year))
                    {
                        decimal smlouvyAllCount = 0;
                        decimal smlouvyPod50kCount = 0;
                        var res = HlidacStatu.Lib.Data.Smlouva.Search.SimpleSearch($"datumUzavreni:[{year}-01-01 TO {year + 1}-01-01}}"
                            , 1, 0, HlidacStatu.Lib.Data.Smlouva.Search.OrderResult.FastestForScroll, null, exactNumOfResults: true);
                        if (res.IsValid)
                            smlouvyAllCount = res.Total;
                        res = HlidacStatu.Lib.Data.Smlouva.Search.SimpleSearch($"cena:>0 AND cena:<=50000 AND datumUzavreni:[{year}-01-01 TO {year + 1}-01-01}}"
                            , 1, 0, HlidacStatu.Lib.Data.Smlouva.Search.OrderResult.FastestForScroll, null, exactNumOfResults: true);
                        if (res.IsValid)
                            smlouvyPod50kCount = res.Total;


                        decimal smlouvyPod50kperc = 0;

                        if (smlouvyAllCount > 0)
                            smlouvyPod50kperc = smlouvyPod50kCount / smlouvyAllCount;

                        totalsAvg50k.Add(year, smlouvyPod50kperc);
                    }
                }
            }
            return totalsAvg50k[year];
        }
        public static decimal SmlouvyPod50kBonus(decimal icoPodil, decimal vsePodil)
        {
            if (icoPodil > vsePodil * 1.75m)
                return 0.75m;
            else if (icoPodil > vsePodil * 1.5m)
                return 0.5m;
            if (icoPodil > vsePodil * 1.25m)
                return 0.25m;
            return 0m;
        }

        class smlouvaStat
        {
            public string Id { get; set; }
            public string IcoDodavatele { get; set; }
            public decimal CastkaSDPH { get; set; }
            public int Rok { get; set; }
            public DateTime Podepsano { get; set; }
        }
        public KoncentraceDodavateluIndexy KoncentraceDodavateluCalculator(string query, decimal? prumHodnotaSmlouvy = null)
        {
            Func<int, int, Nest.ISearchResponse<Lib.Data.Smlouva>> searchFunc = (size, page) =>
                {
                    return Lib.ES.Manager.GetESClient().Search<Lib.Data.Smlouva>(a => a
                                .Size(size)
                                .Source(ss => ss.Excludes(sml => sml.Field(ff => ff.Prilohy)))
                                .From(page * size)
                                .Query(q => Lib.Data.Smlouva.Search.GetSimpleQuery(query))
                                .Scroll("1m")
                                );
                };

            List<smlouvaStat> smlStat = new List<smlouvaStat>();
            Searching.Tools.DoActionForQuery<Lib.Data.Smlouva>(Lib.ES.Manager.GetESClient(),
                searchFunc,
                (h, o) =>
                {
                    Smlouva s = h.Source;
                    if (s != null)
                    {
                        foreach (var prij in s.Prijemce)
                        {
                            if (prij.ico == s.Platce.ico)
                                continue;
                            Firma f = Firmy.Get(prij.ico);
                            if (f.Valid && f.PatrimStatu())
                                continue;

                            string dodavatel = prij.ico;
                            if (string.IsNullOrWhiteSpace(dodavatel))
                                dodavatel = prij.nazev;
                            if (string.IsNullOrWhiteSpace(dodavatel))
                                dodavatel = "neznamy";

                            smlStat.Add(new smlouvaStat()
                            {
                                Id = s.Id,
                                IcoDodavatele = dodavatel,
                                CastkaSDPH = (decimal)s.CalculatedPriceWithVATinCZK / (decimal)s.Prijemce.Length,
                                Podepsano = s.datumUzavreni,
                                Rok = s.datumUzavreni.Year
                            });

                        }
                    }

                    return new Devmasters.Core.Batch.ActionOutputData();
                }, null,
                null, null,
                false, blockSize: 100);

            IEnumerable<SmlouvyForIndex> smlouvy = smlStat
                .Select(m => new SmlouvyForIndex(m.IcoDodavatele, prumHodnotaSmlouvy ?? m.CastkaSDPH))
                .OrderByDescending(m => m.HodnotaSmlouvy) //just better debug
                .ToArray(); //just better debug


            if (smlouvy.Count() == 0)
                return null;
            if (smlouvy.Count() < minPocetSmluvKoncentraceDodavatelu)
                return null;

            var ret = new KoncentraceDodavateluIndexy();
            ret.PocetSmluv = smlouvy.Count();
            ret.PocetSmluvBezCeny = smlouvy.Count(m => m.HodnotaSmlouvy == 0);
            ret.PrumernaHodnotaSmluv = smlouvy
                                .Where(m => m.HodnotaSmlouvy != 0)
                                .Count() == 0 ? 0 : smlouvy
                                                        .Where(m => m.HodnotaSmlouvy != 0)
                                                        .Select(m => Math.Abs(m.HodnotaSmlouvy))
                                                        .Average();
            ret.CelkovaHodnotaSmluv = smlouvy.Sum(m => m.HodnotaSmlouvy);
            ret.Query = query;


            ret.Herfindahl_Hirschman_Index = Herfindahl_Hirschman_Index(smlouvy);
            ret.Herfindahl_Hirschman_Normalized = Herfindahl_Hirschman_IndexNormalized(smlouvy);
            ret.Herfindahl_Hirschman_Modified = Herfindahl_Hirschman_Modified(smlouvy);

            ret.Comprehensive_Industrial_Concentration_Index = Comprehensive_Industrial_Concentration_Index(smlouvy);
            ret.Hall_Tideman_Index = Hall_Tideman_Index(smlouvy);
            ret.Kwoka_Dominance_Index = Kwoka_Dominance_Index(smlouvy);
            ret.LastUpdated = DateTime.Now;
            return ret;
        }


    }

}
