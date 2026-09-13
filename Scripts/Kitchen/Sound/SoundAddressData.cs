using System.Collections.Generic;

public readonly struct SoundAddressData
{
    readonly string[] _addresses;

    public string Key { get; }
    public int AddressCount => _addresses != null ? _addresses.Length : 0;

    public SoundAddressData(string key, params string[] addresses)
    {
        Key = key;
        _addresses = addresses;
    }

    public string GetAddress(int index)
    {
        if (_addresses == null || index < 0 || index >= _addresses.Length)
        {
            return null;
        }

        return _addresses[index];
    }

    public void CollectAddresses(List<string> results)
    {
        if (results == null || _addresses == null)
        {
            return;
        }

        for (var i = 0; i < _addresses.Length; i++)
        {
            var address = _addresses[i];
            if (!string.IsNullOrEmpty(address))
            {
                results.Add(address);
            }
        }
    }
}
