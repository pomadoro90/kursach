using System;

namespace FlareSystem
{
    [Serializable]
    public class FlareDataRecord
    {
        public int N;
        public float P_flare;
        public float Q_flare;
        public float P_purge;
        public float Q_purge;
        public float T_flame;
        public float Steam_Q;
        public int otriv;
        public int hlopok;

        public override string ToString()
        {
            return $"N={N}; P_flare={P_flare:0.###}; Q_flare={Q_flare:0.###}; P_purge={P_purge:0.###}; Q_purge={Q_purge:0.###}; T_flame={T_flame:0.#}; Steam_Q={Steam_Q:0.###}; otriv={otriv}; hlopok={hlopok}";
        }
    }
}
