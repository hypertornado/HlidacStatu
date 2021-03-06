﻿using System;


namespace HlidacStatu.Lib.Data
{
    public partial class Graph
    {
        public class Node : IComparable<Node>
        {
            public int CompareTo(Node other)
            {
                if (other == null)
                    return 1;
                if (this.UniqId == other.UniqId)
                    return 0;
                else
                    return -1;
            }


            public string UniqId
            {
                get
                {
                    if (Type == NodeType.Person)
                        return "p-" + Id;
                    else
                        return "c-" + Id;
                }
            }
            public enum NodeType { Company, Person }
            public string Id { get; set; }
            public NodeType Type { get; set; }
            public string Name { get; set; }
            public bool Highlighted { get; set; }


            public string PrintName(bool html = false)
            {
                switch (Type)
                {
                    case NodeType.Person:
                        return Osoby.GetById.Get(Convert.ToInt32(Id))?.FullNameWithYear(html) ?? "(neznámá osoba)";
                    case NodeType.Company:
                    default:
                        var f = Firmy.Get(Id);
                        if (f.Valid)
                            return f.Jmeno;
                        else
                            return "(neznámá firma)";
                }
            }


        }

    }

}
