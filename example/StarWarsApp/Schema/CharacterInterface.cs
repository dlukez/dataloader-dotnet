using GraphQL.Types;

namespace StarWarsApp.Schema
{
    public class CharacterInterface : InterfaceGraphType
    {
        public CharacterInterface()
        {
            Name = "Character";
            Field<StringGraphType>("name", "The name of the character.");
            Field<ListGraphType<CharacterInterface>>("friends");
        }
    }
}
