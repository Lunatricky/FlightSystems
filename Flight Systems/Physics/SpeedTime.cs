using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameScript.Physics
{
    public class SpeedTime
    {
        public double Speed { get; set; }
        public double Time { get; set; }

        public SpeedTime(double speed, double time)
        {
            Speed = speed;
            Time = time;
        }
    }
}
