using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Genelib {
    [ProtoContract]
    public struct NameGroupMapping {
        [ProtoMember(1)]
        private readonly Tuple<string, int>[] groups;

        private int geneCount = -1;
        public int GeneCount { get => geneCount != -1 ? geneCount : geneCount = groups.Sum(x => x.Item2); }

        public int GeneGroupCount { get => groups.Length; }

        public int GroupOrdinal(string name) {
            for (int i = 0; i < groups.Length; ++i) {
                if (groups[i].Item1 == name) {
                    return i;
                }
            }
            throw new ArgumentException(name + " was not found among gene group names");
        }

        public int GroupSize(string name) {
            foreach (var entry in groups) {
                if (entry.Item1 == name) {
                    return entry.Item2;
                }
            }
            throw new ArgumentException(name + " was not found among gene group names");
        }

        public Range GeneRange(string name) {
            Range result = TryGetRange(name);
            if (result.End.Value == 0) {
                throw new ArgumentException(name + " was not found among gene group names");
            }
            return (Range)result;
        }

        public Range TryGetRange(string name) {
            int start = 0;
            for (int i = 0; i < groups.Length; ++i) {
                if (groups[i].Item1 == name) {
                    int end = start + groups[i].Item2;
                    return start..end;
                }
                start += groups[i].Item2;
            }
            return 0..0;
        }

        public NameGroupMapping() {
            groups = new Tuple<string, int>[0];
        }

        public NameGroupMapping(Tuple<string, int>[] groups) {
            this.groups = groups;
        }
    }
}
