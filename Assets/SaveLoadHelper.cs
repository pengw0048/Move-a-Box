using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using System;
using System.Text;

public static class SaveLoadHelper
{
    public static List<string> GetSaveFileList()
    {
        return new DirectoryInfo(Application.persistentDataPath).GetFiles().Select(f=>f.Name)
            .Where(n => n.EndsWith(".sav")).Select(o => o.Substring(0, o.Length - 4)).ToList();
    }
    public static void Save(string name)
    {
        var objList = new Dictionary<Saveable, SavedItem>();
        var savedList = new List<SavedItem>();
        int i = 0;
        foreach (var obj in UnityEngine.Object.FindObjectsOfType<Saveable>())
        {
            var item = new SavedItem() { prefab = obj.prefab, index = i++ };
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
            objList.Add(obj, item);
            savedList.Add(item);
        }
        foreach (var obj in UnityEngine.Object.FindObjectsOfType<GroupHolder>())
        {
            var item = new SavedItem() { prefab = "Group Holder", index = i++ };
            item.components.Add("Transform");
            item.values.Add("Position", obj.gameObject.transform.position.Serialize());
            item.values.Add("Rotation", obj.gameObject.transform.rotation.eulerAngles.Serialize());
            item.values.Add("Scale", obj.gameObject.transform.localScale.Serialize());
            item.components.Add("GroupHolder");
            var children = new StringBuilder();
            foreach (Transform child in obj.gameObject.transform)
            {
                children.Append(objList[child.gameObject.GetComponent<Saveable>()].index);
                children.Append(' ');
            }
            item.values.Add("Children", children.ToString());
            savedList.Add(item);
        }
        using (var fs = File.Create(Path.Combine(Application.persistentDataPath, name + ".sav")))
            new BinaryFormatter().Serialize(fs, savedList);
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
    public int index;
    public string prefab;
    public List<string> components = new List<string>();
    public Dictionary<string, string> values = new Dictionary<string, string>();
}
