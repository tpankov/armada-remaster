using System.Reflection;
using System.IO;
using System;
using UnityEngine;

public class DynamicLoader
{
    public static void PopulateObjectFromFile(object obj, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            Type objectType = obj.GetType();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue; //skip empty lines, and lines without =

                string[] parts = line.Split('=');
                if (parts.Length != 2) continue; //skip lines with wrong format.

                string fieldName = parts[0].Trim();
                string fieldValue = parts[1].Trim();

                FieldInfo field = objectType.GetField(fieldName);

                if (field != null)
                {
                    try
                    {
                        object convertedValue = Convert.ChangeType(fieldValue, field.FieldType);
                        field.SetValue(obj, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error setting field {fieldName}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Field {fieldName} not found in object.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
        }
    }
}