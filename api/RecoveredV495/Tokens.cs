using System;
using System.Security.Cryptography;
using System.Text;

internal sealed class Tokens
{
	private readonly byte[] secret = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("APP_SECRET") ?? throw new InvalidOperationException("Falta APP_SECRET en .env.local."));

	public string Create(string id)
	{
		return id + "." + Sign(id);
	}

	public bool Verify(string token, string id)
	{
		string[] array = token.Split('.');
		string value;
		string s;
		if (array.Length == 2 && array[0] == id)
		{
			value = id;
			s = array[1];
		}
		else
		{
			if (array.Length != 3 || !(array[0] == id) || !long.TryParse(array[1], out var _))
			{
				return false;
			}
			value = array[0] + "." + array[1];
			s = array[2];
		}
		return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(value)), Encoding.ASCII.GetBytes(s));
	}

	private string Sign(string value)
	{
		return Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
	}
}
