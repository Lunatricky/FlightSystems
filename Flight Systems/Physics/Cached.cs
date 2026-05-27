using System;

namespace IngameScript.Physics
{
    public struct Cached<T>
    {
        private T value;
        public double timeStamp;

        public T Get(double currentTime, Func<T> provider)
        {
            if (timeStamp == currentTime) return value;
            value = provider();
            timeStamp = currentTime;
            return value;
        }

        public void Invalidate()
        {
            timeStamp = double.NaN;
        }
    }
}
