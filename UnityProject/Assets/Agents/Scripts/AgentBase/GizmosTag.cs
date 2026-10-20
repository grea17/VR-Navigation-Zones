public enum GizmosTag
{
    Wall,
    NewTarget,
    TakenTarget,
    Agent,
    Obiettivo       // waypoint intermedio sequenziale
}

public static class MyGizmosTagExtensions
{
    public static GizmosTag ToMyGizmosTag(this Tag tag, bool taken = false)
    {
        return
            tag == Tag.Wall ? GizmosTag.Wall :
            (tag == Tag.Target && !taken) ? GizmosTag.NewTarget :
            (tag == Tag.Target && taken) ? GizmosTag.TakenTarget :
            tag == Tag.Agent ? GizmosTag.Agent :
            tag == Tag.Objective ? GizmosTag.Obiettivo :
            throw new System.NotImplementedException($"GizmosTag: {tag} not implemented");
    }
}