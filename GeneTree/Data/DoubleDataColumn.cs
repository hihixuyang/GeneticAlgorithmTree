﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace GeneTree
{
	public class DoubleDataColumn : DataColumn
	{
		public double _min = double.MaxValue;
		public double _max = double.MinValue;
		public double _range = 0.0;
		
		
		public override double GetTestValue(Random rando)
		{
			return rando.NextDouble() * (_max - _min) + _min;
		}

		public DoubleDataColumn()
		{
			this._type = DataValueTypes.NUMBER;
		}

		void ComputeMinMaxRange()
		{
			_min = _values.Where(c=>!c._isMissing).Min(x => x._value);
			_max = _values.Where(c=>!c._isMissing).Max(x => x._value);
			_range = (_max - _min);
		}
		public override void ProcessRanges()
		{
			ComputeMinMaxRange();
			
			//do a normalization step
			for (int i = 0; i < _values.Count; i++)
			{
				if (_values[i]._isMissing)
				{
					//assumes min = 0
					_values[i]._value = _min - 1;
				}
			}
		}
		public override string GetSummaryString()
		{
			return string.Format("[DoubleDataColumn Min={0}, Max={1}, Header={2}]", _min, _max, this._header);
		}
	}
}




