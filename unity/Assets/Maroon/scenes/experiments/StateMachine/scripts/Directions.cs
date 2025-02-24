﻿using System.Collections;
using System.Collections.Generic;

namespace Maroon.CSE.StateMachine
{
    public class Directions : IEnumerable
    {
        private List<Direction> _directions = new List<Direction>();

        public void AddDirection(Direction direction)
        {
            _directions.Add(direction);
        }

        public IEnumerator GetEnumerator()
        {
            return _directions.GetEnumerator();
        }

        public Direction FindDirection(string name)
        {
            return _directions.Find(element => element.GetDirectionName() == name);
        }
    }
}
