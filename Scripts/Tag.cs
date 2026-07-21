using Godot;

public partial class Tag
{
    public string Name { get; }
    public string Category { get; }
    public Color Colour { get; }

    public Tag(string name, string category = Globals.DefaultTagType, Color? colour = null)
    {
        Name = name;
        Category = category;
        Colour = colour.GetValueOrDefault(Colors.White);
    }

    public override int GetHashCode()
    {
        return (Name + "_" + Category).GetHashCode();
    }

    public override string ToString()
    {
        return Name;
    }

    public override bool Equals(object obj)
    {
        if (obj is not Tag)
        {
            return base.Equals(obj);
        } 
        else
        {
            var other = obj as Tag;
            return other.Name.Equals(Name, System.StringComparison.InvariantCultureIgnoreCase) && other.Category.Equals(Category, System.StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
