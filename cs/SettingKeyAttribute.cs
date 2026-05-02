using System;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingKeyAttribute : Attribute
{
    public string Key { get; }
    public SettingKeyAttribute(string key) => Key = key;
}