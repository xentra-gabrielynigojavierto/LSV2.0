export default function EnrollWelcomePage() {
  return (
    <main className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 flex items-center justify-center px-4">
      <div className="max-w-lg w-full text-center">

        {/* Success icon */}
        <div className="inline-flex items-center justify-center w-20 h-20 rounded-full bg-green-100 mb-6">
          <i className="ri-checkbox-circle-fill text-4xl text-green-600" />
        </div>

        <h1 className="text-3xl font-bold text-gray-900 mb-3">
          Welcome to CareConnect!
        </h1>
        <p className="text-gray-500 mb-8 leading-relaxed">
          Your account is ready. You can now sign in to manage referrals,
          view appointments, and communicate with referring law firms — all
          from your provider portal.
        </p>

        <div className="space-y-3">
          <a
            href="https://careconnect-demo.legalsynq.com/login"
            className="block w-full py-3 px-6 rounded-xl bg-blue-600 text-white font-semibold text-sm hover:bg-blue-700 transition-colors"
          >
            <i className="ri-login-box-line mr-2" />
            Sign In to Your Portal
          </a>
          <a
            href="/network"
            className="block w-full py-3 px-6 rounded-xl border border-gray-300 text-gray-700 font-semibold text-sm hover:bg-gray-50 transition-colors"
          >
            <i className="ri-arrow-left-line mr-2" />
            Back to Network Directory
          </a>
        </div>

        <div className="mt-10 bg-blue-50 rounded-xl p-5 text-left">
          <h2 className="text-sm font-semibold text-blue-900 mb-2">What happens next?</h2>
          <ul className="space-y-2 text-sm text-blue-700">
            <li className="flex items-start gap-2">
              <i className="ri-check-line flex-shrink-0 mt-0.5 text-blue-500" />
              Sign in with the email and password you just created.
            </li>
            <li className="flex items-start gap-2">
              <i className="ri-check-line flex-shrink-0 mt-0.5 text-blue-500" />
              Your profile is already pre-filled from the network directory.
            </li>
            <li className="flex items-start gap-2">
              <i className="ri-check-line flex-shrink-0 mt-0.5 text-blue-500" />
              Start receiving and managing referrals from your dashboard.
            </li>
          </ul>
        </div>
      </div>
    </main>
  );
}
