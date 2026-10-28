using System;

namespace Frontier
{
    [Serializable]
    public struct ItemAmount
    {
        public ResourceSO resource;
        public int amount;

        public ItemAmount(ResourceSO resource, int amount)
        {
            this.resource = resource;
            this.amount = amount;
        }
    }
}
