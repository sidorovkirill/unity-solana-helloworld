using System.Collections.Generic;

namespace Phantom
{
	public class DeepLinkData
	{
		public string Method { get; set; }
		public Dictionary<string, string> Params = new();

		public override string ToString()
		{
			var res = $"Method: {this.Method}";

			foreach (var param in this.Params)
			{
				res += $"\nKey: {param.Key}, val: {param.Value}";
			}
            
			return res;
		}
	}
}