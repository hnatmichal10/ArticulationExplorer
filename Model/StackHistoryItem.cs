using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArticulationExplorer.Model
{
    public class StackHistoryItem<T>
    {
        public T? Item { get; set; }
        public bool IsRemoved { get; set; }
    }
}
