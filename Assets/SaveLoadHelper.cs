using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using System;

public static class SaveLoadHelper
{
    public static List<string> GetSaveFileList()
    {
        return new DirectoryInfo(Application.persistentDataPath).GetFiles().Select(f=>f.Name)
            .Where(n => n.EndsWith(".sav")).Select(o => o.Substring(0, o.Length - 4)).ToList();
    }
    public static void Save(string name)
    {
        var objList = new List<SavedItem>();
        foreach (var obj in UnityEngine.Object.FindObjectsOfType<Saveable>())
        {
            var item = new SavedItem() { prefab = obj.prefab };
            item.components.Add("Transform");
            item.values.Add("Position", obj.gameObject.transform.position.Serialize());
            item.values.Add("Rotation", obj.gameObject.transform.rotation.eulerAngles.Serialize());
            item.values.Add("Scale", obj.gameObject.transform.localScale.Serialize());
            var gen = obj.gameObject.GetComponent<ResourceGenerator>();
            if (gen != null)
            {
                item.components.Add("ResourceGenerator");
                item.values.Add("Remain", gen.remain.ToString());
            }
            objList.Add(item);
        }
        using (var fs = File.Create(Path.Combine(Application.persistentDataPath, name + ".sav")))
            new BinaryFormatter().Serialize(fs, objList);
    }
    public static List<SavedItem> Load(string name)
    {
        using (var fs = File.Open(Path.Combine(Application.persistentDataPath, name + ".sav"), FileMode.Open))
            return new BinaryFormatter().Deserialize(fs) as List<SavedItem>;
    }
}
public static class Extensions
{
    public static string Serialize(this Vector3 vec)
    {
        return vec.x + " " + vec.y + " " + vec.z;
    }
    public static Vector3 DeserializeVector3(this string str)
    {
        var vec = new Vector3();
        var tokens = str.Split(' ');
        vec.x = float.Parse(tokens[0]);
        vec.y = float.Parse(tokens[1]);
        vec.z = float.Parse(tokens[2]);
        return vec;
    }
}
[Serializable]
public class SavedItem
{
    public string prefab;
    public List<string> components = new List<string>();
    public Dictionary<string, string> values = new Dictionary<string, string>();
}
