using System.Collections.Generic;

namespace IngameScript.Physics
{
    public class SpeedTimeTracker
    {
        List<SpeedTime> speedTimeValues;
        const int SpeedTimeTrackerMaxSize = 100;

        public SpeedTimeTracker()
        {
            speedTimeValues = new List<SpeedTime>();
        }

        public void AddValue(double speed, double time)
        {
            if (speedTimeValues.Count >= SpeedTimeTrackerMaxSize)
            {
                speedTimeValues.RemoveAt(0); // Remove the oldest
            }
            speedTimeValues.Add(new SpeedTime(speed, time));
        }

        public double GetAverageSpeed()
        {
            double avgSpeed = 0;
            foreach (var value in speedTimeValues)
            {
                avgSpeed += value.Speed;
            }
            return avgSpeed / speedTimeValues.Count;
        }
    }
}
