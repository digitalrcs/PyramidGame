using Pyramid.Web;

public static class VerificationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"pyramid-verification-"+Guid.NewGuid().ToString("N"));var db=new Storage(root);
        db.Create("a","Alpha","alpha-hash","alpha@example.test");db.Create("admin","Keeper","admin-hash");db.BootstrapAdmin("Keeper");
        check(db.FindSignIn("ALPHA@EXAMPLE.TEST")?.Id=="a"&&db.FindLogin("alpha@example.test")==null,"Unique pending email can locate limited sign-in account but cannot request password recovery");
        check(db.FindById("a")?.EmailVerified==false,"Pending email sign-in lookup never grants verification");
        db.Create("b","Bravo","bravo-hash","alpha@example.test");
        check(db.FindSignIn("alpha@example.test")==null&&db.FindSignIn("Alpha")?.Id=="a"&&db.FindSignIn("Bravo")?.Id=="b","Ambiguous pending email fails closed while explorer-name sign-in remains available");
        db.IssueAccountToken("a","verify","verify-alpha",DateTimeOffset.UtcNow.AddMinutes(10));db.ConsumeVerificationToken("verify-alpha");
        check(db.FindSignIn("alpha@example.test")?.Id=="a","Verified email ownership takes precedence over another pending claimant");
        db.SetPendingEmail("a","changed@example.test");
        check(db.FindById("a")!.EmailVerified&&db.FindSignIn("alpha@example.test")?.Id=="a"&&db.FindSignIn("changed@example.test")==null,"Pending email change preserves existing verification and verified login address");
        db.SetPendingEmail("b","bravo@example.test");db.AdminApproveOrReset("admin","b","approve","Bravo","bravo@example.test");
        check(db.FindById("b")!.EmailVerified&&db.FindSignIn("bravo@example.test")?.Id=="b","Manual administrator approval satisfies the same verification state as an email token");
        check(!db.FindById("admin")!.EmailVerified&&db.FindById("admin")!.IsAdmin,"Administrator role does not silently verify an email address");
    }
}
