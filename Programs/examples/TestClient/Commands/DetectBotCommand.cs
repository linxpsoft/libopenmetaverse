using System;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenMetaverse.TestClient
{
    public class DetectBotCommand : Command
    {
        public DetectBotCommand(TestClient testClient)
        {
            Name = "detectbot";
            Description = "Runs in the background, reporting any potential bots";
            Category = CommandCategory.TestClient;

            testClient.Avatars.OnAvatarAppearance += new AvatarManager.AvatarAppearanceCallback(Avatars_OnAvatarAppearance);
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return "This command is always running";
        }

        void Avatars_OnAvatarAppearance(UUID avatarID, bool isTrial, LLObject.TextureEntryFace defaultTexture, LLObject.TextureEntryFace[] faceTextures, System.Collections.Generic.List<byte> visualParams)
        {
            if (IsNullOrZero(faceTextures[(int)AppearanceManager.TextureIndex.EyesBaked]) &&
                IsNullOrZero(faceTextures[(int)AppearanceManager.TextureIndex.HeadBaked]) &&
                IsNullOrZero(faceTextures[(int)AppearanceManager.TextureIndex.LowerBaked]) &&
                IsNullOrZero(faceTextures[(int)AppearanceManager.TextureIndex.SkirtBaked]) &&
                IsNullOrZero(faceTextures[(int)AppearanceManager.TextureIndex.UpperBaked]))
            {
                Console.WriteLine("Avatar " + avatarID.ToString() + " may be a bot");
            }
        }

        private bool IsNullOrZero(LLObject.TextureEntryFace face)
        {
            return (face == null || face.TextureID == UUID.Zero);
        }
    }
}
